using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using AccountingSystem.API.Controllers;
using AccountingSystem.API.Data;
using AccountingSystem.API.Identity;
using AccountingSystem.API.Middleware;
using AccountingSystem.API.Models;
using AccountingSystem.API.Security;
using AccountingSystem.API.Services;
using AccountingSystem.API.Services.Interfaces;
using AccountingSystem.Shared.DTOs;
using AccountingSystem.Shared.Validation;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Moq;

namespace AccountingSystem.API.Tests;

public class PasswordPolicyTests
{
    [Theory]
    [InlineData("LongPassword123!")]
    [InlineData("Solar winds gather softly")]
    public void TryValidate_WhenPasswordMeetsPolicy_ShouldReturnTrue(string password)
    {
        var isValid = PasswordPolicy.TryValidate(password, out var errorMessage);

        isValid.Should().BeTrue();
        errorMessage.Should().BeEmpty();
    }

    [Theory]
    [InlineData("short1!")]
    [InlineData("alllowercasepassword")]
    [InlineData("two words only")]
    public void TryValidate_WhenPasswordIsWeak_ShouldReturnFalse(string password)
    {
        var isValid = PasswordPolicy.TryValidate(password, out var errorMessage);

        isValid.Should().BeFalse();
        errorMessage.Should().NotBeNullOrWhiteSpace();
    }
}

public class LegacyPasswordServiceTests
{
    [Fact]
    public void CreateHash_AndTryVerify_WhenPasswordMatches_ShouldSucceed()
    {
        var service = new LegacyPasswordService();
        var passwordData = service.CreateHash("LongPassword123!");

        var isUsable = service.TryVerify(
            "LongPassword123!",
            passwordData.PasswordHash,
            passwordData.PasswordSalt,
            out var passwordMatches);

        isUsable.Should().BeTrue();
        passwordMatches.Should().BeTrue();
    }

    [Fact]
    public void TryVerify_WhenStoredPasswordDataIsMalformed_ShouldReturnFalse()
    {
        var service = new LegacyPasswordService();

        var isUsable = service.TryVerify("LongPassword123!", "not-base64", "still-not-base64", out var passwordMatches);

        isUsable.Should().BeFalse();
        passwordMatches.Should().BeFalse();
    }
}

public class JwtAuthTokenFactoryTests
{
    [Fact]
    public void Create_WhenCalled_ShouldPreserveExistingJwtClaimContract()
    {
        var configuration = TestHelpers.CreateConfiguration();
        var factory = new JwtAuthTokenFactory(configuration);

        var result = factory.Create(new AuthTokenContext(
            "user@example.com",
            "Admin",
            123,
            "Test User",
            456,
            "Contoso"));

        var token = new JwtSecurityTokenHandler().ReadJwtToken(result.Token);

        GetClaimValue(token, ClaimTypes.Name, JwtRegisteredClaimNames.UniqueName).Should().Be("user@example.com");
        GetClaimValue(token, ClaimTypes.Role, "role").Should().Be("Admin");
        token.Claims.First(c => c.Type == "UserId").Value.Should().Be("123");
        token.Claims.First(c => c.Type == "role").Value.Should().Be("Admin");
        token.Claims.First(c => c.Type == "FullName").Value.Should().Be("Test User");
        token.Claims.First(c => c.Type == "CompanyId").Value.Should().Be("456");
        token.Claims.First(c => c.Type == "CompanyName").Value.Should().Be("Contoso");
        result.ExpiresAt.Should().BeAfter(DateTime.UtcNow.AddMinutes(59));
    }

    private static string GetClaimValue(JwtSecurityToken token, params string[] claimTypes)
    {
        return token.Claims.First(c => claimTypes.Contains(c.Type, StringComparer.Ordinal)).Value;
    }
}

public class SharedPasswordIdentityValidatorTests
{
    [Fact]
    public async Task ValidateAsync_WhenPasswordMatchesSharedPolicy_ShouldSucceed()
    {
        using var harness = TestHelpers.CreateIdentityHarness();
        var validator = new SharedPasswordIdentityValidator();

        var result = await validator.ValidateAsync(harness.UserManager, new ApplicationUser(), "Solar winds gather softly");

        result.Succeeded.Should().BeTrue();
    }
}

public class AuthServiceTests
{
    [Fact]
    public async Task LoginAsync_WhenIdentityBackedUserExists_ShouldAuthenticateWithoutLegacyPassword()
    {
        var context = TestHelpers.CreateContext(tenantId: 10);
        using var harness = TestHelpers.CreateIdentityHarness();
        var service = TestHelpers.CreateAuthService(context, harness);

        var role = new Role { Id = 1, Name = "Admin" };
        var company = new Company { Id = 10, Name = "Contoso", IsActive = true, Status = "Active" };
        var user = TestHelpers.CreateUser(role, company.Id, "admin@contoso.com", "UnusedLegacy123!");
        user.PasswordHash = "corrupted";
        user.PasswordSalt = "corrupted";

        context.Roles.Add(role);
        context.Companies.Add(company);
        context.Users.Add(user);
        await context.SaveChangesAsync();

        await harness.AccountService.EnsureProvisionedAsync(
            TestHelpers.CreateIdentitySnapshot(user, role.Name),
            "LongPassword123!");

        var response = await service.LoginAsync(new LoginDTO
        {
            Email = "admin@contoso.com",
            Password = "LongPassword123!"
        });

        response.Token.Should().NotBeNullOrWhiteSpace();
        response.CompanyId.Should().Be(company.Id);
        response.Role.Should().Be("Admin");

        var token = new JwtSecurityTokenHandler().ReadJwtToken(response.Token);
        token.Claims.First(c => c.Type == "UserId").Value.Should().Be(user.Id.ToString());
        token.Claims.First(c => c.Type == "CompanyId").Value.Should().Be(company.Id.ToString());
        token.Claims.First(c => c.Type == "CompanyName").Value.Should().Be(company.Name);

        var reloadedUser = await context.Users.IgnoreQueryFilters().SingleAsync(u => u.Id == user.Id);
        reloadedUser.PasswordHash.Should().BeEmpty();
        reloadedUser.PasswordSalt.Should().BeNull();
    }

    [Fact]
    public async Task LoginAsync_WhenLegacyOnlyUserSignsIn_ShouldProvisionIdentityAndClearLegacyPassword()
    {
        var context = TestHelpers.CreateContext(tenantId: 11);
        using var harness = TestHelpers.CreateIdentityHarness();
        var service = TestHelpers.CreateAuthService(context, harness);

        var role = new Role { Id = 2, Name = "Accounting" };
        var company = new Company { Id = 11, Name = "Hydrate Co", IsActive = true, Status = "Active" };
        var user = TestHelpers.CreateUser(role, company.Id, "hydrate@test.com", "LongPassword123!");

        context.Roles.Add(role);
        context.Companies.Add(company);
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var response = await service.LoginAsync(new LoginDTO
        {
            Email = "hydrate@test.com",
            Password = "LongPassword123!"
        });

        response.Email.Should().Be("hydrate@test.com");

        var identityUser = await harness.IdentityContext.Users.SingleAsync(u => u.LegacyUserId == user.Id);
        identityUser.Email.Should().Be("hydrate@test.com");
        identityUser.CompanyId.Should().Be(company.Id);
        identityUser.FullName.Should().Be(user.FullName);
        (await harness.UserManager.CheckPasswordAsync(identityUser, "LongPassword123!")).Should().BeTrue();
        (await harness.UserManager.GetRolesAsync(identityUser)).Should().ContainSingle("Accounting");

        var reloadedLegacyUser = await context.Users.IgnoreQueryFilters().SingleAsync(u => u.Id == user.Id);
        reloadedLegacyUser.PasswordHash.Should().BeEmpty();
        reloadedLegacyUser.PasswordSalt.Should().BeNull();
    }

    [Fact]
    public async Task RegisterCompanyAsync_WhenSuccessful_ShouldCreateLegacyAndIdentityAdmin()
    {
        var context = TestHelpers.CreateContext();
        using var harness = TestHelpers.CreateIdentityHarness();
        var captcha = new Mock<ICaptchaService>();
        captcha.Setup(x => x.VerifyTokenAsync(It.IsAny<string>())).ReturnsAsync(true);
        var service = TestHelpers.CreateAuthService(context, harness, captcha: captcha);

        context.Roles.Add(new Role { Id = 1, Name = "Admin" });
        await context.SaveChangesAsync();

        var response = await service.RegisterCompanyAsync(new CompanyRegisterDTO
        {
            CompanyName = "Phase Six Co",
            AdminEmail = "owner@phasesix.com",
            AdminFullName = "Owner User",
            Password = "LongPassword123!",
            RecaptchaToken = "good-token"
        });

        response.Role.Should().Be("Admin");
        response.CompanyName.Should().Be("Phase Six Co");

        var company = await context.Companies.IgnoreQueryFilters().SingleAsync(c => c.Name == "Phase Six Co");
        var legacyUser = await context.Users.IgnoreQueryFilters().Include(u => u.Role).SingleAsync(u => u.Email == "owner@phasesix.com");
        legacyUser.CompanyId.Should().Be(company.Id);
        legacyUser.Role.Name.Should().Be("Admin");
        legacyUser.PasswordHash.Should().BeEmpty();
        legacyUser.PasswordSalt.Should().BeNull();

        var identityUser = await harness.IdentityContext.Users.SingleAsync(u => u.LegacyUserId == legacyUser.Id);
        (await harness.UserManager.CheckPasswordAsync(identityUser, "LongPassword123!")).Should().BeTrue();
        (await harness.UserManager.GetRolesAsync(identityUser)).Should().ContainSingle("Admin");
    }

    [Fact]
    public async Task RegisterAsync_WhenSuccessful_ShouldCreateIdentityUserAndLeaveLegacyPasswordCleared()
    {
        var context = TestHelpers.CreateContext(tenantId: 77);
        using var harness = TestHelpers.CreateIdentityHarness();
        var service = TestHelpers.CreateAuthService(context, harness);

        context.Roles.Add(new Role { Id = 2, Name = "Accounting" });
        await context.SaveChangesAsync();

        var user = await service.RegisterAsync(new RegisterDTO
        {
            Email = "new.accountant@test.com",
            FullName = "New Accountant",
            Password = "LongPassword123!",
            RoleName = "Accounting"
        });

        user.CompanyId.Should().Be(77);
        user.PasswordHash.Should().BeEmpty();
        user.PasswordSalt.Should().BeNull();

        var identityUser = await harness.IdentityContext.Users.SingleAsync(u => u.LegacyUserId == user.Id);
        identityUser.CompanyId.Should().Be(77);
        identityUser.Email.Should().Be("new.accountant@test.com");
        (await harness.UserManager.GetRolesAsync(identityUser)).Should().ContainSingle("Accounting");
        (await harness.UserManager.CheckPasswordAsync(identityUser, "LongPassword123!")).Should().BeTrue();
    }

    [Fact]
    public async Task ChangePasswordAsync_WhenIdentityUserExists_ShouldUpdateIdentityPasswordAndClearLegacyPassword()
    {
        var context = TestHelpers.CreateContext(tenantId: 12);
        using var harness = TestHelpers.CreateIdentityHarness();
        var service = TestHelpers.CreateAuthService(context, harness);

        var role = new Role { Id = 1, Name = "Admin" };
        var company = new Company { Id = 12, Name = "Password Co", IsActive = true, Status = "Active" };
        var user = TestHelpers.CreateUser(role, company.Id, "password@test.com", "LongPassword123!");

        context.Roles.Add(role);
        context.Companies.Add(company);
        context.Users.Add(user);
        await context.SaveChangesAsync();

        await harness.AccountService.EnsureProvisionedAsync(
            TestHelpers.CreateIdentitySnapshot(user, role.Name),
            "LongPassword123!");

        await service.ChangePasswordAsync(user.Id, new ChangePasswordDTO
        {
            CurrentPassword = "LongPassword123!",
            NewPassword = "BetterPassword456!",
            ConfirmPassword = "BetterPassword456!"
        });

        var identityUser = await harness.IdentityContext.Users.SingleAsync(u => u.LegacyUserId == user.Id);
        (await harness.UserManager.CheckPasswordAsync(identityUser, "LongPassword123!")).Should().BeFalse();
        (await harness.UserManager.CheckPasswordAsync(identityUser, "BetterPassword456!")).Should().BeTrue();

        var reloadedUser = await context.Users.IgnoreQueryFilters().SingleAsync(u => u.Id == user.Id);
        reloadedUser.PasswordHash.Should().BeEmpty();
        reloadedUser.PasswordSalt.Should().BeNull();
    }

    [Fact]
    public async Task UpdateProfileAsync_AndGetCurrentProfileAsync_ShouldPersistIdentityAndLegacyValues()
    {
        var context = TestHelpers.CreateContext(tenantId: 13);
        using var harness = TestHelpers.CreateIdentityHarness();
        var service = TestHelpers.CreateAuthService(context, harness);

        var role = new Role { Id = 1, Name = "Admin" };
        var company = new Company { Id = 13, Name = "Profile Co", IsActive = true, Status = "Active" };
        var user = TestHelpers.CreateUser(role, company.Id, "profile@test.com", "LongPassword123!");

        context.Roles.Add(role);
        context.Companies.Add(company);
        context.Users.Add(user);
        await context.SaveChangesAsync();

        await harness.AccountService.EnsureProvisionedAsync(
            TestHelpers.CreateIdentitySnapshot(user, role.Name),
            "LongPassword123!");

        await service.UpdateProfileAsync(user.Id, new UpdateProfileDTO
        {
            Email = "updated.profile@test.com",
            FullName = "Updated Profile"
        });

        var profile = await service.GetCurrentProfileAsync(user.Id);
        profile.Email.Should().Be("updated.profile@test.com");
        profile.FullName.Should().Be("Updated Profile");
        profile.Role.Should().Be("Admin");
        profile.CompanyName.Should().Be(company.Name);

        var identityUser = await harness.IdentityContext.Users.SingleAsync(u => u.LegacyUserId == user.Id);
        identityUser.Email.Should().Be("updated.profile@test.com");
        identityUser.UserName.Should().Be("updated.profile@test.com");
        identityUser.FullName.Should().Be("Updated Profile");
    }

    [Fact]
    public async Task ForgotAndResetPassword_WhenLegacyOnlyAccountExists_ShouldProvisionIdentitySendEmailAndResetPassword()
    {
        var context = TestHelpers.CreateContext(tenantId: 14);
        using var harness = TestHelpers.CreateIdentityHarness();
        var service = TestHelpers.CreateAuthService(context, harness);

        var role = new Role { Id = 3, Name = "Management" };
        var company = new Company { Id = 14, Name = "Reset Co", IsActive = true, Status = "Active" };
        var user = TestHelpers.CreateUser(role, company.Id, "reset@test.com", "LongPassword123!");

        context.Roles.Add(role);
        context.Companies.Add(company);
        context.Users.Add(user);
        await context.SaveChangesAsync();

        await service.SendPasswordResetAsync(new ForgotPasswordDTO { Email = user.Email });

        harness.EmailService.SentEmails.Should().ContainSingle();
        var sentEmail = harness.EmailService.SentEmails.Single();
        sentEmail.Email.Should().Be("reset@test.com");
        sentEmail.ResetLink.Should().Contain("/reset-password?");

        var query = QueryHelpers.ParseQuery(new Uri(sentEmail.ResetLink).Query);
        var encodedToken = query["token"].ToString();
        var encodedEmail = query["email"].ToString();
        encodedToken.Should().NotBeNullOrWhiteSpace();
        encodedEmail.Should().NotBeNullOrWhiteSpace();

        await service.ResetPasswordAsync(new ResetPasswordDTO
        {
            Email = encodedEmail,
            Token = encodedToken,
            NewPassword = "BetterPassword456!",
            ConfirmPassword = "BetterPassword456!"
        });

        var identityUser = await harness.IdentityContext.Users.SingleAsync(u => u.LegacyUserId == user.Id);
        (await harness.UserManager.CheckPasswordAsync(identityUser, "BetterPassword456!")).Should().BeTrue();

        var reloadedUser = await context.Users.IgnoreQueryFilters().SingleAsync(u => u.Id == user.Id);
        reloadedUser.PasswordHash.Should().BeEmpty();
        reloadedUser.PasswordSalt.Should().BeNull();

        var loginResponse = await service.LoginAsync(new LoginDTO
        {
            Email = user.Email,
            Password = "BetterPassword456!"
        });
        loginResponse.Token.Should().NotBeNullOrWhiteSpace();
    }
}

public class JwtMiddlewareTests
{
    [Fact]
    public async Task Invoke_WhenTokenIsExpired_ShouldNotAttachUserToContext()
    {
        var configuration = TestHelpers.CreateConfiguration(clockSkewSeconds: 0);
        var token = TestHelpers.CreateJwtToken(configuration, DateTime.UtcNow.AddMinutes(-5));
        var nextCalled = false;
        var middleware = new JwtMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        }, configuration);

        var context = new DefaultHttpContext();
        context.Request.Headers.Authorization = $"Bearer {token}";

        await middleware.Invoke(context);

        nextCalled.Should().BeTrue();
        context.Items.ContainsKey("UserId").Should().BeFalse();
        context.Items.ContainsKey("Role").Should().BeFalse();
        context.Items.ContainsKey("CompanyId").Should().BeFalse();
    }
}

public class AuthControllerTests
{
    [Theory]
    [InlineData("UnknownUser")]
    [InlineData("CorruptedLegacyPassword")]
    [InlineData("BadIdentityPassword")]
    public async Task Login_WhenAuthenticationFails_ShouldReturnGenericUnauthorizedPayload(string scenario)
    {
        var context = TestHelpers.CreateContext(tenantId: 30);
        using var harness = TestHelpers.CreateIdentityHarness();
        var service = TestHelpers.CreateAuthService(context, harness);

        var role = new Role { Id = 1, Name = "Admin" };
        var company = new Company { Id = 30, Name = "Northwind", IsActive = true, Status = "Active" };
        context.Roles.Add(role);
        context.Companies.Add(company);

        if (scenario != "UnknownUser")
        {
            var user = TestHelpers.CreateUser(role, company.Id, "user@northwind.com", "LongPassword123!");

            if (scenario == "CorruptedLegacyPassword")
            {
                user.PasswordHash = "bad";
                user.PasswordSalt = "bad";
            }

            context.Users.Add(user);
            await context.SaveChangesAsync();

            if (scenario == "BadIdentityPassword")
            {
                await harness.AccountService.EnsureProvisionedAsync(
                    TestHelpers.CreateIdentitySnapshot(user, role.Name),
                    "LongPassword123!");
                user.PasswordHash = string.Empty;
                user.PasswordSalt = null;
                await context.SaveChangesAsync();
            }
        }
        else
        {
            await context.SaveChangesAsync();
        }

        var controller = new AuthController(service);
        var response = await controller.Login(new LoginDTO
        {
            Email = scenario == "UnknownUser" ? "missing@northwind.com" : "user@northwind.com",
            Password = "WrongPassword123!"
        });

        var unauthorized = response.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        TestHelpers.GetAnonymousStringValue(unauthorized.Value, "error")
            .Should().Be("Invalid email or password. Please try again later.");
    }

    [Fact]
    public async Task RegisterCompany_WhenServiceSucceeds_ShouldReturnOk()
    {
        var authService = new Mock<IAuthService>();
        authService.Setup(x => x.RegisterCompanyAsync(It.IsAny<CompanyRegisterDTO>()))
            .ReturnsAsync(new AuthResponseDTO { Email = "ok@test.com" });
        var controller = new AuthController(authService.Object);

        var response = await controller.RegisterCompany(new CompanyRegisterDTO());

        response.Should().BeOfType<OkObjectResult>();
    }
}

public class CaptchaServiceTests
{
    [Fact]
    public async Task VerifyTokenAsync_WhenGoogleReturnsSuccessfulScore_ShouldReturnTrue()
    {
        var httpClient = new HttpClient(new StubMessageHandler(HttpStatusCode.OK, "{\"success\":true,\"score\":0.9}"));
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Recaptcha:SecretKey"] = "secret",
            ["Recaptcha:ScoreThreshold"] = "0.5"
        }).Build();

        var service = new CaptchaService(httpClient, config);

        var result = await service.VerifyTokenAsync("token");

        result.Should().BeTrue();
    }

    [Fact]
    public async Task VerifyTokenAsync_WhenRequestThrows_ShouldReturnFalse()
    {
        var httpClient = new HttpClient(new ThrowingMessageHandler());
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Recaptcha:SecretKey"] = "secret"
        }).Build();
        var service = new CaptchaService(httpClient, config);

        var result = await service.VerifyTokenAsync("token");

        result.Should().BeFalse();
    }

    private sealed class StubMessageHandler(HttpStatusCode statusCode, string content) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(content, Encoding.UTF8, "application/json")
            });
    }

    private sealed class ThrowingMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new HttpRequestException("network failed");
    }
}

internal static class TestHelpers
{
    internal static AccountingDbContext CreateContext(int tenantId = 0)
    {
        var tenant = new Mock<ITenantService>();
        tenant.Setup(x => x.GetCurrentTenant()).Returns(tenantId);

        var options = new DbContextOptionsBuilder<AccountingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AccountingDbContext(options, tenant.Object);
    }

    internal static IConfiguration CreateConfiguration(int clockSkewSeconds = 60)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JwtSettings:Secret"] = "super-secret-key-for-tests-only-1234567890",
                ["JwtSettings:Issuer"] = "issuer",
                ["JwtSettings:Audience"] = "audience",
                ["JwtSettings:ExpiryMinutes"] = "60",
                ["JwtSettings:ClockSkewSeconds"] = clockSkewSeconds.ToString(),
                ["AuthSecurity:Lockout:MaxFailedAccessAttempts"] = "5",
                ["AuthSecurity:Lockout:LockoutMinutes"] = "15",
                ["AppUrls:ClientBaseUrl"] = "https://client.example.test"
            })
            .Build();
    }

    internal static User CreateUser(
        Role role,
        int companyId,
        string email,
        string password,
        bool isActive = true,
        string status = "Active")
    {
        using var hmac = new HMACSHA512();
        var salt = hmac.Key;
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(password));

        return new User
        {
            CompanyId = companyId,
            Email = email,
            FullName = "Test User",
            RoleId = role.Id,
            Role = role,
            PasswordHash = Convert.ToBase64String(hash),
            PasswordSalt = Convert.ToBase64String(salt),
            IsActive = isActive,
            Status = status
        };
    }

    internal static LegacyIdentityUserSnapshot CreateIdentitySnapshot(User user, string roleName) =>
        new(
            user.Id,
            user.CompanyId,
            user.Email,
            user.FullName ?? user.Email,
            user.Status,
            user.IsActive,
            user.IsDeleted,
            roleName);

    internal static AuthService CreateAuthService(
        AccountingDbContext context,
        IdentityTestHarness harness,
        IConfiguration? configuration = null,
        Mock<ICaptchaService>? captcha = null,
        Mock<IAuthSecurityAuditService>? auditService = null)
    {
        configuration ??= CreateConfiguration();
        captcha ??= new Mock<ICaptchaService>();
        captcha.Setup(x => x.VerifyTokenAsync(It.IsAny<string>())).ReturnsAsync(true);

        auditService ??= new Mock<IAuthSecurityAuditService>();
        auditService.Setup(x => x.WriteAsync(
                It.IsAny<string>(),
                It.IsAny<int?>(),
                It.IsAny<int?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<int?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<string?>()))
            .Returns(Task.CompletedTask);

        return new AuthService(
            context,
            harness.IdentityContext,
            configuration,
            captcha.Object,
            Mock.Of<ILogger<AuthService>>(),
            auditService.Object,
            new LegacyPasswordService(),
            new JwtAuthTokenFactory(configuration),
            harness.AccountService,
            harness.EmailService,
            harness.UserManager);
    }

    internal static string CreateJwtToken(IConfiguration configuration, DateTime expiresAtUtc)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.ASCII.GetBytes(configuration["JwtSettings:Secret"]!);
        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.Name, "user@example.com"),
                new Claim(ClaimTypes.Role, "Admin"),
                new Claim("UserId", "123"),
                new Claim("role", "Admin"),
                new Claim("CompanyId", "456")
            }),
            NotBefore = expiresAtUtc.AddHours(-1),
            Expires = expiresAtUtc,
            Issuer = configuration["JwtSettings:Issuer"],
            Audience = configuration["JwtSettings:Audience"],
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };

        return tokenHandler.WriteToken(tokenHandler.CreateToken(descriptor));
    }

    internal static string? GetAnonymousStringValue(object? source, string propertyName)
    {
        return source?.GetType().GetProperty(propertyName)?.GetValue(source)?.ToString();
    }

    internal static IdentityTestHarness CreateIdentityHarness()
    {
        return new IdentityTestHarness();
    }
}

internal sealed class IdentityTestHarness : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly IServiceScope _scope;

    public IdentityTestHarness()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDataProtection();

        services.AddDbContext<IdentityAuthDbContext>(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services.Configure<DataProtectionTokenProviderOptions>(options =>
        {
            options.TokenLifespan = TimeSpan.FromHours(2);
        });

        var identityBuilder = services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.Password.RequiredLength = 12;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireDigit = false;
                options.Password.RequiredUniqueChars = 1;

                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                options.Lockout.AllowedForNewUsers = true;

                options.User.RequireUniqueEmail = true;
                options.User.AllowedUserNameCharacters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._@+";
            })
            .AddRoles<ApplicationRole>()
            .AddEntityFrameworkStores<IdentityAuthDbContext>()
            .AddDefaultTokenProviders();

        identityBuilder.AddPasswordValidator<SharedPasswordIdentityValidator>();
        services.AddScoped<IIdentityAccountService, IdentityAccountService>();

        var emailService = new TestAccountEmailService();
        services.AddSingleton(emailService);
        services.AddSingleton<IAccountEmailService>(emailService);

        _serviceProvider = services.BuildServiceProvider();
        _scope = _serviceProvider.CreateScope();

        IdentityContext = _scope.ServiceProvider.GetRequiredService<IdentityAuthDbContext>();
        IdentityContext.Database.EnsureCreated();
        UserManager = _scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        AccountService = _scope.ServiceProvider.GetRequiredService<IIdentityAccountService>();
        EmailService = emailService;
    }

    public IdentityAuthDbContext IdentityContext { get; }

    public UserManager<ApplicationUser> UserManager { get; }

    public IIdentityAccountService AccountService { get; }

    public TestAccountEmailService EmailService { get; }

    public void Dispose()
    {
        _scope.Dispose();
        _serviceProvider.Dispose();
    }
}

internal sealed class TestAccountEmailService : IAccountEmailService
{
    public List<SentResetEmail> SentEmails { get; } = new();

    public Task SendPasswordResetAsync(string email, string fullName, string resetLink, CancellationToken cancellationToken = default)
    {
        SentEmails.Add(new SentResetEmail(email, fullName, resetLink));
        return Task.CompletedTask;
    }
}

internal sealed record SentResetEmail(string Email, string FullName, string ResetLink);
