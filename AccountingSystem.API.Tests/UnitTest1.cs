using System.Net;
using System.Net.Http;
using System.Text;
using AccountingSystem.API.Controllers;
using AccountingSystem.API.Data;
using AccountingSystem.API.Models;
using AccountingSystem.API.Services;
using AccountingSystem.API.Services.Interfaces;
using AccountingSystem.Shared.DTOs;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;

namespace AccountingSystem.API.Tests;

public class AuthServiceTests
{
    [Fact]
    public async Task LoginAsync_WhenCredentialsAreValid_ShouldReturnAuthResponseWithToken()
    {
        var context = CreateContext();
        var config = CreateJwtConfiguration();
        var captcha = new Mock<ICaptchaService>();
        var service = new AuthService(context, config, captcha.Object);

        var role = new Role { Id = 1, Name = "Admin" };
        var company = new Company { Id = 10, Name = "Contoso", IsActive = true, Status = "Active" };
        context.Roles.Add(role);
        context.Companies.Add(company);

        var salt = Encoding.UTF8.GetBytes("12345678901234567890123456789012");
        using var hmac = new System.Security.Cryptography.HMACSHA512(salt);
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes("P@ssword1"));

        context.Users.Add(new User
        {
            Id = 99,
            CompanyId = company.Id,
            Email = "admin@contoso.com",
            FullName = "Admin User",
            RoleId = role.Id,
            Role = role,
            PasswordHash = Convert.ToBase64String(hash),
            PasswordSalt = Convert.ToBase64String(salt),
            IsActive = true,
            Status = "Active"
        });
        await context.SaveChangesAsync();

        var response = await service.LoginAsync(new LoginDTO { Email = "admin@contoso.com", Password = "P@ssword1" });

        response.Token.Should().NotBeNullOrWhiteSpace();
        response.CompanyId.Should().Be(10);
        response.Role.Should().Be("Admin");
    }

    [Fact]
    public async Task LoginAsync_WhenPasswordIsInvalid_ShouldThrowExpectedException()
    {
        var context = CreateContext();
        var config = CreateJwtConfiguration();
        var captcha = new Mock<ICaptchaService>();
        var service = new AuthService(context, config, captcha.Object);

        var role = new Role { Id = 1, Name = "Admin" };
        var company = new Company { Id = 11, Name = "Tailspin", IsActive = true, Status = "Active" };
        context.Roles.Add(role);
        context.Companies.Add(company);
        context.Users.Add(new User
        {
            CompanyId = company.Id,
            Email = "wrong@tailspin.com",
            Role = role,
            RoleId = role.Id,
            PasswordHash = Convert.ToBase64String(new byte[64]),
            PasswordSalt = Convert.ToBase64String(new byte[64]),
            IsActive = true,
            Status = "Active"
        });
        await context.SaveChangesAsync();

        var action = () => service.LoginAsync(new LoginDTO { Email = "wrong@tailspin.com", Password = "invalid" });

        await action.Should().ThrowAsync<Exception>().WithMessage("Invalid email or password.");
    }

    [Fact]
    public async Task RegisterCompanyAsync_WhenCaptchaFails_ShouldThrowSecurityException()
    {
        var context = CreateContext();
        var config = CreateJwtConfiguration();
        var captcha = new Mock<ICaptchaService>();
        captcha.Setup(x => x.VerifyTokenAsync(It.IsAny<string>())).ReturnsAsync(false);

        var service = new AuthService(context, config, captcha.Object);
        var dto = new CompanyRegisterDTO
        {
            CompanyName = "New Co",
            AdminEmail = "owner@newco.com",
            AdminFullName = "Owner",
            Password = "StrongPassword1!",
            RecaptchaToken = "bad-token"
        };

        var action = () => service.RegisterCompanyAsync(dto);

        await action.Should().ThrowAsync<Exception>().WithMessage("Security check failed. Automated activity detected.");
    }

    private static AccountingDbContext CreateContext()
    {
        var tenant = new Mock<ITenantService>();
        tenant.Setup(x => x.GetCurrentTenant()).Returns(0);
        var options = new DbContextOptionsBuilder<AccountingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AccountingDbContext(options, tenant.Object);
    }

    private static IConfiguration CreateJwtConfiguration() => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["JwtSettings:Secret"] = "super-secret-key-for-tests-only-1234567890",
            ["JwtSettings:Issuer"] = "issuer",
            ["JwtSettings:Audience"] = "audience",
            ["JwtSettings:ExpiryMinutes"] = "60"
        })
        .Build();
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
            Task.FromResult(new HttpResponseMessage(statusCode) { Content = new StringContent(content, Encoding.UTF8, "application/json") });
    }

    private sealed class ThrowingMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new HttpRequestException("network failed");
    }
}

public class AuthControllerTests
{
    [Fact]
    public async Task Login_WhenServiceThrows_ShouldReturnUnauthorized()
    {
        var authService = new Mock<IAuthService>();
        authService.Setup(x => x.LoginAsync(It.IsAny<LoginDTO>())).ThrowsAsync(new Exception("bad creds"));
        var controller = new AuthController(authService.Object);

        var response = await controller.Login(new LoginDTO { Email = "a", Password = "b" });

        response.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task RegisterCompany_WhenServiceSucceeds_ShouldReturnOk()
    {
        var authService = new Mock<IAuthService>();
        authService.Setup(x => x.RegisterCompanyAsync(It.IsAny<CompanyRegisterDTO>())).ReturnsAsync(new AuthResponseDTO { Email = "ok@test.com" });
        var controller = new AuthController(authService.Object);

        var response = await controller.RegisterCompany(new CompanyRegisterDTO());

        response.Should().BeOfType<OkObjectResult>();
    }
}

public class SuperAdminControllerTests
{
    [Fact]
    public async Task UpdateCompanyStatus_WhenCompanyDoesNotExist_ShouldReturnNotFound()
    {
        var controller = new SuperAdminController(CreateContext());

        var response = await controller.UpdateCompanyStatus(404, new UpdateCompanyStatusDTO { Status = "Active" });

        response.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task UpdateCompanyStatus_WhenStatusIsInvalid_ShouldReturnBadRequest()
    {
        var context = CreateContext();
        context.Companies.Add(new Company { Id = 7, Name = "Tenant 7", Status = "Active", IsActive = true });
        await context.SaveChangesAsync();
        var controller = new SuperAdminController(context);

        var response = await controller.UpdateCompanyStatus(7, new UpdateCompanyStatusDTO { Status = "Invalid" });

        response.Should().BeOfType<BadRequestObjectResult>();
    }

    private static AccountingDbContext CreateContext()
    {
        var tenant = new Mock<ITenantService>();
        tenant.Setup(x => x.GetCurrentTenant()).Returns(0);
        var options = new DbContextOptionsBuilder<AccountingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AccountingDbContext(options, tenant.Object);
    }
}
