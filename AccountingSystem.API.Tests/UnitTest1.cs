using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using AccountingSystem.API.Controllers;
using AccountingSystem.API.Data;
using AccountingSystem.API.Middleware;
using AccountingSystem.API.Models;
using AccountingSystem.API.Services;
using AccountingSystem.API.Services.Interfaces;
using AccountingSystem.Shared.DTOs;
using AccountingSystem.Shared.Validation;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
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

    private static AuthService CreateAuthService(
        AccountingDbContext context,
        IConfiguration? configuration = null,
        Mock<ICaptchaService>? captcha = null,
        Mock<IAuthSecurityAuditService>? auditService = null)
    {
        configuration ??= TestHelpers.CreateConfiguration();
        captcha ??= new Mock<ICaptchaService>();
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
            configuration,
            captcha.Object,
            Mock.Of<ILogger<AuthService>>(),
            auditService.Object);
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

public class SuperAdminControllerTests
{
    [Fact]
    public async Task UpdateCompanyStatus_WhenCompanyDoesNotExist_ShouldReturnNotFound()
    {
        var controller = new SuperAdminController(TestHelpers.CreateContext(), Mock.Of<ILogger<SuperAdminController>>());

        var response = await controller.UpdateCompanyStatus(404, new UpdateCompanyStatusDTO { Status = "Active" });

        response.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task UpdateCompanyStatus_WhenStatusIsInvalid_ShouldReturnBadRequest()
    {
        var context = TestHelpers.CreateContext();
        context.Companies.Add(new Company { Id = 7, Name = "Tenant 7", Status = "Active", IsActive = true });
        await context.SaveChangesAsync();
        var controller = new SuperAdminController(context, Mock.Of<ILogger<SuperAdminController>>());

        var response = await controller.UpdateCompanyStatus(7, new UpdateCompanyStatusDTO { Status = "Invalid" });

        response.Should().BeOfType<BadRequestObjectResult>();
    }
}

internal static class TestHelpers
{
    internal static AccountingDbContext CreateContext()
    {
        var tenant = new Mock<ITenantService>();
        tenant.Setup(x => x.GetCurrentTenant()).Returns(0);

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

    internal static AuthService CreateAuthService(AccountingDbContext context, IConfiguration configuration)
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

        return new AuthService(
            context,
            configuration,
            captcha.Object,
            Mock.Of<ILogger<AuthService>>(),
            auditService.Object);
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
}
