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
        var userManager = harness.UserManager;

        var result = await validator.ValidateAsync(userManager, new ApplicationUser(), "Solar winds gather softly");

        result.Succeeded.Should().BeTrue();
    }
}

public class AuthServiceTests
{
    [Fact]
    public async Task LoginAsync_WhenCredentialsAreValid_ShouldResetFailedAttemptsAndReturnToken()
    {
        var context = TestHelpers.CreateContext();
        var service = CreateAuthService(context);

        var role = new Role { Id = 1, Name = "Admin" };
        var company = new Company { Id = 10, Name = "Contoso", IsActive = true, Status = "Active" };
        context.Roles.Add(role);
        context.Companies.Add(company);
        context.Users.Add(TestHelpers.CreateUser(
            role,
            company.Id,
            "admin@contoso.com",
            "LongPassword123!",
            accessFailedCount: 3));
        await context.SaveChangesAsync();

        var response = await service.LoginAsync(new LoginDTO
        {
            Email = "admin@contoso.com",
            Password = "LongPassword123!"
        });

        response.Token.Should().NotBeNullOrWhiteSpace();
        response.CompanyId.Should().Be(company.Id);
        response.Role.Should().Be("Admin");

        var reloadedUser = await context.Users.IgnoreQueryFilters().SingleAsync(u => u.Email == "admin@contoso.com");
        reloadedUser.AccessFailedCount.Should().Be(0);
        reloadedUser.LockoutEndUtc.Should().BeNull();
    }

    [Fact]
    public async Task LoginAsync_WhenLegacyUserHasNoIdentityRecord_ShouldHydrateIdentityUser()
    {
        var context = TestHelpers.CreateContext();
        using var harness = TestHelpers.CreateIdentityHarness();
        var service = CreateAuthService(context, identityBridgeService: harness.BridgeService);

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
        identityUser.Status.Should().Be(user.Status);
        (await harness.UserManager.CheckPasswordAsync(identityUser, "LongPassword123!")).Should().BeTrue();
        (await harness.UserManager.GetRolesAsync(identityUser)).Should().ContainSingle("Accounting");
    }

    [Fact]
    public async Task LoginAsync_WhenFailedAttemptsReachThreshold_ShouldApplyTemporaryLockout()
    {
        var context = TestHelpers.CreateContext();
        var service = CreateAuthService(context);

        var role = new Role { Id = 1, Name = "Admin" };
        var company = new Company { Id = 20, Name = "Tailspin", IsActive = true, Status = "Active" };
        context.Roles.Add(role);
        context.Companies.Add(company);
        context.Users.Add(TestHelpers.CreateUser(
            role,
            company.Id,
            "locked@tailspin.com",
            "LongPassword123!",
            accessFailedCount: 4));
        await context.SaveChangesAsync();

        var action = () => service.LoginAsync(new LoginDTO
        {
            Email = "locked@tailspin.com",
            Password = "WrongPassword123!"
        });

        await action.Should().ThrowAsync<Exception>()
            .WithMessage("Invalid email or password. Please try again later.");

        var reloadedUser = await context.Users.IgnoreQueryFilters().SingleAsync(u => u.Email == "locked@tailspin.com");
        reloadedUser.AccessFailedCount.Should().Be(5);
        reloadedUser.LockoutEndUtc.Should().NotBeNull();
        reloadedUser.LockoutEndUtc.Should().BeAfter(DateTime.UtcNow.AddMinutes(14));
    }

    [Fact]
    public async Task RegisterCompanyAsync_WhenCaptchaFails_ShouldThrowSecurityException()
    {
        var context = TestHelpers.CreateContext();
        var captcha = new Mock<ICaptchaService>();
        captcha.Setup(x => x.VerifyTokenAsync(It.IsAny<string>())).ReturnsAsync(false);

        var service = CreateAuthService(context, captcha: captcha);
        var dto = new CompanyRegisterDTO
        {
            CompanyName = "New Co",
            AdminEmail = "owner@newco.com",
            AdminFullName = "Owner",
            Password = "LongPassword123!",
            RecaptchaToken = "bad-token"
        };

        var action = () => service.RegisterCompanyAsync(dto);

        await action.Should().ThrowAsync<Exception>()
            .WithMessage("Security check failed. Automated activity detected.");
    }

    [Fact]
    public async Task RegisterAsync_WhenSuccessful_ShouldProvisionIdentityUserInParallel()
    {
        var context = TestHelpers.CreateContext(tenantId: 77);
        using var harness = TestHelpers.CreateIdentityHarness();
        var service = CreateAuthService(context, identityBridgeService: harness.BridgeService);

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

        var identityUser = await harness.IdentityContext.Users.SingleAsync(u => u.LegacyUserId == user.Id);
        identityUser.CompanyId.Should().Be(77);
        identityUser.Email.Should().Be("new.accountant@test.com");
        (await harness.UserManager.GetRolesAsync(identityUser)).Should().ContainSingle("Accounting");
    }

    [Fact]
    public async Task ChangePasswordAsync_WhenIdentityUserIsMissing_ShouldCreateAndSyncIdentityPassword()
    {
        var context = TestHelpers.CreateContext();
        using var harness = TestHelpers.CreateIdentityHarness();
        var service = CreateAuthService(context, identityBridgeService: harness.BridgeService);

        var role = new Role { Id = 1, Name = "Admin" };
        var company = new Company { Id = 12, Name = "Password Co", IsActive = true, Status = "Active" };
        var user = TestHelpers.CreateUser(role, company.Id, "password@test.com", "LongPassword123!");
        context.Roles.Add(role);
        context.Companies.Add(company);
        context.Users.Add(user);
        await context.SaveChangesAsync();

        await service.ChangePasswordAsync(user.Id, new ChangePasswordDTO
        {
            CurrentPassword = "LongPassword123!",
            NewPassword = "BetterPassword456!",
            ConfirmPassword = "BetterPassword456!"
        });

        var identityUser = await harness.IdentityContext.Users.SingleAsync(u => u.LegacyUserId == user.Id);
        (await harness.UserManager.CheckPasswordAsync(identityUser, "BetterPassword456!")).Should().BeTrue();
        identityUser.Email.Should().Be("password@test.com");
    }

    [Fact]
    public async Task UpdateProfileAsync_WhenIdentityUserExists_ShouldSyncEmailAndFullName()
    {
        var context = TestHelpers.CreateContext();
        using var harness = TestHelpers.CreateIdentityHarness();
        var service = CreateAuthService(context, identityBridgeService: harness.BridgeService);

        var role = new Role { Id = 1, Name = "Admin" };
        var company = new Company { Id = 13, Name = "Profile Co", IsActive = true, Status = "Active" };
        var user = TestHelpers.CreateUser(role, company.Id, "profile@test.com", "LongPassword123!");
        context.Roles.Add(role);
        context.Companies.Add(company);
        context.Users.Add(user);
        await context.SaveChangesAsync();

        await harness.AccountService.EnsureProvisionedAsync(
            new LegacyIdentityUserSnapshot(user.Id, company.Id, user.Email, user.FullName, user.Status, user.IsActive, user.IsDeleted, role.Name),
            "LongPassword123!");

        await service.UpdateProfileAsync(user.Id, new UpdateProfileDTO
        {
            Email = "updated.profile@test.com",
            FullName = "Updated Profile"
        });

        var identityUser = await harness.IdentityContext.Users.SingleAsync(u => u.LegacyUserId == user.Id);
        identityUser.Email.Should().Be("updated.profile@test.com");
        identityUser.UserName.Should().Be("updated.profile@test.com");
        identityUser.FullName.Should().Be("Updated Profile");
    }

    private static AuthService CreateAuthService(
        AccountingDbContext context,
        IConfiguration? configuration = null,
        Mock<ICaptchaService>? captcha = null,
        Mock<IAuthSecurityAuditService>? auditService = null,
        ILegacyIdentityBridgeService? identityBridgeService = null)
    {
        configuration ??= TestHelpers.CreateConfiguration();
        captcha ??= new Mock<ICaptchaService>();
        auditService ??= new Mock<IAuthSecurityAuditService>();
        identityBridgeService ??= Mock.Of<ILegacyIdentityBridgeService>();

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
            configuration,
            captcha.Object,
            Mock.Of<ILogger<AuthService>>(),
            auditService.Object,
            new LegacyPasswordService(),
            new JwtAuthTokenFactory(configuration),
            identityBridgeService);
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
    [InlineData("InvalidPassword")]
    [InlineData("BlockedUser")]
    [InlineData("DeactivatedUser")]
    [InlineData("LockoutActive")]
    public async Task Login_WhenAuthenticationFails_ShouldReturnGenericUnauthorizedPayload(string scenario)
    {
        var context = TestHelpers.CreateContext();
        var configuration = TestHelpers.CreateConfiguration();
        var role = new Role { Id = 1, Name = "Admin" };
        var company = new Company { Id = 30, Name = "Northwind", IsActive = true, Status = "Active" };
        context.Roles.Add(role);
        context.Companies.Add(company);

        if (scenario != "UnknownUser")
        {
            var user = TestHelpers.CreateUser(role, company.Id, "user@northwind.com", "LongPassword123!");

            switch (scenario)
            {
                case "BlockedUser":
                    user.Status = "Blocked";
                    break;
                case "DeactivatedUser":
                    user.IsActive = false;
                    break;
                case "LockoutActive":
                    user.AccessFailedCount = 5;
                    user.LockoutEndUtc = DateTime.UtcNow.AddMinutes(15);
                    break;
            }

            context.Users.Add(user);
        }

        await context.SaveChangesAsync();

        var controller = new AuthController(TestHelpers.CreateAuthService(context, configuration));
        var loginDto = new LoginDTO
        {
            Email = "user@northwind.com",
            Password = scenario == "InvalidPassword" ? "WrongPassword123!" : "LongPassword123!"
        };

        if (scenario == "UnknownUser")
        {
            loginDto.Email = "missing@northwind.com";
        }

        var response = await controller.Login(loginDto);

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

public class LegacyIdentityBridgeServiceTests
{
    [Fact]
    public async Task SyncAfterSuccessfulLoginAsync_WhenIdentitySyncFails_ShouldAuditAndNotThrow()
    {
        var accountService = new Mock<IIdentityAccountService>();
        accountService.Setup(x => x.EnsureProvisionedAsync(
                It.IsAny<LegacyIdentityUserSnapshot>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("sync failed"));

        var auditService = new Mock<IAuthSecurityAuditService>();
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

        var bridge = new LegacyIdentityBridgeService(
            accountService.Object,
            auditService.Object,
            Mock.Of<ILogger<LegacyIdentityBridgeService>>());

        var action = () => bridge.SyncAfterSuccessfulLoginAsync(
            new LegacyIdentityUserSnapshot(10, 20, "bridge@test.com", "Bridge User", "Active", true, false, "Admin"),
            "LongPassword123!");

        await action.Should().NotThrowAsync();
        auditService.Verify(x => x.WriteAsync(
            "IDENTITY-SYNC-FAILURE",
            10,
            20,
            "bridge@test.com",
            "SuccessfulLogin",
            It.IsAny<int?>(),
            It.IsAny<DateTime?>(),
            "InvalidOperationException"),
            Times.Once);
    }
}

public class SuperAdminControllerTests
{
    [Fact]
    public async Task UpdateCompanyStatus_WhenCompanyDoesNotExist_ShouldReturnNotFound()
    {
        var controller = new SuperAdminController(
            TestHelpers.CreateContext(),
            Mock.Of<ILogger<SuperAdminController>>(),
            Mock.Of<ILegacyIdentityBridgeService>());

        var response = await controller.UpdateCompanyStatus(404, new UpdateCompanyStatusDTO { Status = "Active" });

        response.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task UpdateCompanyStatus_WhenStatusIsInvalid_ShouldReturnBadRequest()
    {
        var context = TestHelpers.CreateContext();
        context.Companies.Add(new Company { Id = 7, Name = "Tenant 7", Status = "Active", IsActive = true });
        await context.SaveChangesAsync();
        var controller = new SuperAdminController(
            context,
            Mock.Of<ILogger<SuperAdminController>>(),
            Mock.Of<ILegacyIdentityBridgeService>());

        var response = await controller.UpdateCompanyStatus(7, new UpdateCompanyStatusDTO { Status = "Invalid" });

        response.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task UpdateUserStatus_WhenSuccessful_ShouldSyncLinkedIdentityUser()
    {
        var context = TestHelpers.CreateContext();
        using var harness = TestHelpers.CreateIdentityHarness();

        var role = new Role { Id = 2, Name = "Accounting" };
        var user = TestHelpers.CreateUser(role, 50, "status@test.com", "LongPassword123!");
        context.Roles.Add(role);
        context.Users.Add(user);
        await context.SaveChangesAsync();

        await harness.AccountService.EnsureProvisionedAsync(
            new LegacyIdentityUserSnapshot(user.Id, user.CompanyId, user.Email, user.FullName, user.Status, user.IsActive, user.IsDeleted, role.Name),
            "LongPassword123!");

        var controller = new SuperAdminController(
            context,
            Mock.Of<ILogger<SuperAdminController>>(),
            harness.BridgeService)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = TestHelpers.CreateHttpContextWithUser(999, "super@test.com", "SuperAdmin", 1)
            }
        };

        var response = await controller.UpdateUserStatus(user.Id, new UpdateUserStatusDTO { Status = "Blocked" });

        response.Should().BeOfType<OkObjectResult>();
        var identityUser = await harness.IdentityContext.Users.SingleAsync(u => u.LegacyUserId == user.Id);
        identityUser.Status.Should().Be("Blocked");
        identityUser.IsActive.Should().BeFalse();
    }
}

public class UsersControllerTests
{
    [Fact]
    public async Task DeleteUser_WhenSuccessful_ShouldSyncLinkedIdentityUser()
    {
        var context = TestHelpers.CreateContext(tenantId: 88);
        using var harness = TestHelpers.CreateIdentityHarness();

        var role = new Role { Id = 2, Name = "Accounting" };
        var user = TestHelpers.CreateUser(role, 88, "archive@test.com", "LongPassword123!");
        context.Roles.Add(role);
        context.Users.Add(user);
        await context.SaveChangesAsync();

        await harness.AccountService.EnsureProvisionedAsync(
            new LegacyIdentityUserSnapshot(user.Id, user.CompanyId, user.Email, user.FullName, user.Status, user.IsActive, user.IsDeleted, role.Name),
            "LongPassword123!");

        var controller = new UsersController(
            context,
            Mock.Of<IAuthService>(),
            harness.BridgeService);

        var response = await controller.DeleteUser(user.Id);

        response.Should().BeOfType<OkObjectResult>();
        var identityUser = await harness.IdentityContext.Users.SingleAsync(u => u.LegacyUserId == user.Id);
        identityUser.IsDeleted.Should().BeTrue();
        identityUser.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task RestoreUser_WhenSuccessful_ShouldSyncLinkedIdentityUser()
    {
        var context = TestHelpers.CreateContext();
        using var harness = TestHelpers.CreateIdentityHarness();

        var role = new Role { Id = 2, Name = "Accounting" };
        var user = TestHelpers.CreateUser(role, 89, "restore@test.com", "LongPassword123!", isActive: false);
        user.IsDeleted = true;
        context.Roles.Add(role);
        context.Users.Add(user);
        await context.SaveChangesAsync();

        await harness.AccountService.EnsureProvisionedAsync(
            new LegacyIdentityUserSnapshot(user.Id, user.CompanyId, user.Email, user.FullName, user.Status, user.IsActive, user.IsDeleted, role.Name),
            "LongPassword123!");

        var controller = new UsersController(
            context,
            Mock.Of<IAuthService>(),
            harness.BridgeService);

        var response = await controller.RestoreUser(user.Id);

        response.Should().BeOfType<OkObjectResult>();
        var identityUser = await harness.IdentityContext.Users.SingleAsync(u => u.LegacyUserId == user.Id);
        identityUser.IsDeleted.Should().BeFalse();
        identityUser.IsActive.Should().BeTrue();
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
            ["AuthSecurity:Lockout:LockoutMinutes"] = "15"
        })
            .Build();
    }

    internal static User CreateUser(
        Role role,
        int companyId,
        string email,
        string password,
        bool isActive = true,
        string status = "Active",
        int accessFailedCount = 0,
        DateTime? lockoutEndUtc = null)
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
            Status = status,
            AccessFailedCount = accessFailedCount,
            LockoutEndUtc = lockoutEndUtc
        };
    }

    internal static AuthService CreateAuthService(
        AccountingDbContext context,
        IConfiguration configuration,
        ILegacyIdentityBridgeService? identityBridgeService = null)
    {
        var captcha = new Mock<ICaptchaService>();
        var auditService = new Mock<IAuthSecurityAuditService>();
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
        identityBridgeService ??= Mock.Of<ILegacyIdentityBridgeService>();

        return new AuthService(
            context,
            configuration,
            captcha.Object,
            Mock.Of<ILogger<AuthService>>(),
            auditService.Object,
            new LegacyPasswordService(),
            new JwtAuthTokenFactory(configuration),
            identityBridgeService);
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

    internal static DefaultHttpContext CreateHttpContextWithUser(int userId, string email, string role, int companyId)
    {
        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim("UserId", userId.ToString()),
            new Claim(ClaimTypes.Name, email),
            new Claim("unique_name", email),
            new Claim(ClaimTypes.Role, role),
            new Claim("role", role),
            new Claim("CompanyId", companyId.ToString())
        }, "TestAuth"));

        return context;
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

        var auditService = new Mock<IAuthSecurityAuditService>();
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

        services.AddSingleton(auditService.Object);
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
        services.AddScoped<ILegacyIdentityBridgeService, LegacyIdentityBridgeService>();

        _serviceProvider = services.BuildServiceProvider();
        _scope = _serviceProvider.CreateScope();

        IdentityContext = _scope.ServiceProvider.GetRequiredService<IdentityAuthDbContext>();
        IdentityContext.Database.EnsureCreated();
        UserManager = _scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        RoleManager = _scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
        AccountService = _scope.ServiceProvider.GetRequiredService<IIdentityAccountService>();
        BridgeService = _scope.ServiceProvider.GetRequiredService<ILegacyIdentityBridgeService>();
    }

    public IdentityAuthDbContext IdentityContext { get; }

    public UserManager<ApplicationUser> UserManager { get; }

    public RoleManager<ApplicationRole> RoleManager { get; }

    public IIdentityAccountService AccountService { get; }

    public ILegacyIdentityBridgeService BridgeService { get; }

    public void Dispose()
    {
        _scope.Dispose();
        _serviceProvider.Dispose();
    }
}
