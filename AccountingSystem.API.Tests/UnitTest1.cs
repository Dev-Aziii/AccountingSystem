using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Buffers.Binary;
using AccountingSystem.API.Controllers;
using AccountingSystem.API.Configuration;
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
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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
            ApplicationRoles.TenantOwner,
            123,
            "Test User",
            456,
            "Contoso"));

        var token = new JwtSecurityTokenHandler().ReadJwtToken(result.Token);

        GetClaimValue(token, ClaimTypes.Name, JwtRegisteredClaimNames.UniqueName).Should().Be("user@example.com");
        GetClaimValue(token, ClaimTypes.Role, "role").Should().Be(ApplicationRoles.TenantOwner);
        token.Claims.First(c => c.Type == "UserId").Value.Should().Be("123");
        token.Claims.First(c => c.Type == "role").Value.Should().Be(ApplicationRoles.TenantOwner);
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

        var role = new Role { Id = 1, Name = ApplicationRoles.TenantOwner };
        var company = new Company { Id = 10, Name = "Contoso", IsActive = true, Status = "Active" };
        var user = TestHelpers.CreateUser(role, company.Id, "admin@contoso.com", "UnusedLegacy123!");
        user.PasswordHash = "corrupted";
        user.PasswordSalt = "corrupted";

        context.Roles.Add(role);
        context.Companies.Add(company);
        context.Users.Add(user);
        await context.SaveChangesAsync();

        await harness.AccountService.EnsureProvisionedAsync(
            TestHelpers.CreateIdentitySnapshot(user, role.Name, requireEmailConfirmation: true, emailConfirmed: true),
            "LongPassword123!");

        var response = await service.LoginAsync(new LoginDTO
        {
            Email = "admin@contoso.com",
            Password = "LongPassword123!"
        });

        response.Token.Should().NotBeNullOrWhiteSpace();
        response.CompanyId.Should().Be(company.Id);
        response.Role.Should().Be(ApplicationRoles.TenantOwner);

        var token = new JwtSecurityTokenHandler().ReadJwtToken(response.Token);
        token.Claims.First(c => c.Type == "UserId").Value.Should().Be(user.Id.ToString());
        token.Claims.First(c => c.Type == "CompanyId").Value.Should().Be(company.Id.ToString());
        token.Claims.First(c => c.Type == "CompanyName").Value.Should().Be(company.Name);

        var reloadedUser = await context.Users.IgnoreQueryFilters().SingleAsync(u => u.Id == user.Id);
        reloadedUser.PasswordHash.Should().BeEmpty();
        reloadedUser.PasswordSalt.Should().BeNull();
    }

    [Fact]
    public async Task LoginAsync_WhenLegacyOnlyNonSuperAdminSignsIn_ShouldProvisionIdentityClearLegacyPasswordAndDenyUntilConfirmed()
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

        var act = async () => await service.LoginAsync(new LoginDTO
        {
            Email = "hydrate@test.com",
            Password = "LongPassword123!"
        });

        var exception = await Record.ExceptionAsync(act);
        exception.Should().NotBeNull();
        exception!.Message.Should().Be("Invalid email or password. Please try again later.");

        var identityUser = await harness.IdentityContext.Users.SingleAsync(u => u.LegacyUserId == user.Id);
        identityUser.Email.Should().Be("hydrate@test.com");
        identityUser.CompanyId.Should().Be(company.Id);
        identityUser.FullName.Should().Be(user.FullName);
        identityUser.RequireEmailConfirmation.Should().BeTrue();
        identityUser.EmailConfirmed.Should().BeFalse();
        (await harness.UserManager.CheckPasswordAsync(identityUser, "LongPassword123!")).Should().BeTrue();
        (await harness.UserManager.GetRolesAsync(identityUser)).Should().ContainSingle("Accounting");

        var reloadedLegacyUser = await context.Users.IgnoreQueryFilters().SingleAsync(u => u.Id == user.Id);
        reloadedLegacyUser.PasswordHash.Should().BeEmpty();
        reloadedLegacyUser.PasswordSalt.Should().BeNull();
    }

    [Fact]
    public async Task RegisterCompanyAsync_WhenSuccessful_ShouldCreateLegacyAndIdentityTenantOwner()
    {
        var context = TestHelpers.CreateContext();
        using var harness = TestHelpers.CreateIdentityHarness();
        var captcha = new Mock<ICaptchaService>();
        captcha.Setup(x => x.VerifyTokenAsync(It.IsAny<string>())).ReturnsAsync(true);
        var service = TestHelpers.CreateAuthService(context, harness, captcha: captcha);

        context.Roles.Add(new Role { Id = 1, Name = ApplicationRoles.TenantOwner });
        await context.SaveChangesAsync();

        var response = await service.RegisterCompanyAsync(new CompanyRegisterDTO
        {
            CompanyName = "Phase Six Co",
            AdminEmail = "owner@phasesix.com",
            AdminFullName = "Owner User",
            Password = "LongPassword123!",
            RecaptchaToken = "good-token"
        });

        response.Role.Should().Be(ApplicationRoles.TenantOwner);
        response.CompanyName.Should().Be("Phase Six Co");
        response.Token.Should().BeEmpty();
        response.RequiresEmailConfirmation.Should().BeTrue();
        response.Message.Should().Contain("confirm your email");

        var company = await context.Companies.IgnoreQueryFilters().SingleAsync(c => c.Name == "Phase Six Co");
        var legacyUser = await context.Users.IgnoreQueryFilters().Include(u => u.Role).SingleAsync(u => u.Email == "owner@phasesix.com");
        legacyUser.CompanyId.Should().Be(company.Id);
        legacyUser.Role.Name.Should().Be(ApplicationRoles.TenantOwner);
        legacyUser.PasswordHash.Should().BeEmpty();
        legacyUser.PasswordSalt.Should().BeNull();

        var identityUser = await harness.IdentityContext.Users.SingleAsync(u => u.LegacyUserId == legacyUser.Id);
        (await harness.UserManager.CheckPasswordAsync(identityUser, "LongPassword123!")).Should().BeTrue();
        (await harness.UserManager.GetRolesAsync(identityUser)).Should().ContainSingle(ApplicationRoles.TenantOwner);
        identityUser.RequireEmailConfirmation.Should().BeTrue();
        identityUser.EmailConfirmed.Should().BeFalse();
        harness.EmailService.SentConfirmationEmails.Should().ContainSingle();
        harness.EmailService.SentConfirmationEmails.Single().ConfirmationLink.Should().Contain("/confirm-email?");
    }

    [Fact]
    public async Task RegisterCompanyAsync_WhenDevelopmentOriginDiffersFromConfiguredClientBaseUrl_ShouldUseRequestOriginForConfirmationLink()
    {
        var context = TestHelpers.CreateContext();
        using var harness = TestHelpers.CreateIdentityHarness();
        var captcha = new Mock<ICaptchaService>();
        captcha.Setup(x => x.VerifyTokenAsync(It.IsAny<string>())).ReturnsAsync(true);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JwtSettings:Secret"] = "super-secret-key-for-tests-only-1234567890",
                ["JwtSettings:Issuer"] = "issuer",
                ["JwtSettings:Audience"] = "audience",
                ["JwtSettings:ExpiryMinutes"] = "60",
                ["JwtSettings:ClockSkewSeconds"] = "60",
                ["AuthSecurity:Lockout:MaxFailedAccessAttempts"] = "5",
                ["AuthSecurity:Lockout:LockoutMinutes"] = "15",
                ["IdentityTokens:PasswordResetTokenLifespanMinutes"] = "120",
                ["IdentityTokens:EmailConfirmationTokenLifespanMinutes"] = "1440",
                ["AppUrls:ClientBaseUrl"] = "https://localhost:5173"
            })
            .Build();

        var httpContextAccessor = TestHelpers.CreateHttpContextAccessor(
            scheme: "https",
            host: "localhost:7273",
            origin: "https://localhost:7273");

        var service = TestHelpers.CreateAuthService(
            context,
            harness,
            configuration: configuration,
            captcha: captcha,
            httpContextAccessor: httpContextAccessor);

        context.Roles.Add(new Role { Id = 1, Name = ApplicationRoles.TenantOwner });
        await context.SaveChangesAsync();

        await service.RegisterCompanyAsync(new CompanyRegisterDTO
        {
            CompanyName = "Origin Co",
            AdminEmail = "owner@originco.com",
            AdminFullName = "Owner User",
            Password = "LongPassword123!",
            RecaptchaToken = "good-token"
        });

        harness.EmailService.SentConfirmationEmails.Should().ContainSingle();
        harness.EmailService.SentConfirmationEmails.Single().ConfirmationLink
            .Should().StartWith("https://localhost:7273/confirm-email?");
    }

    [Fact]
    public async Task InviteTenantUserAsync_WhenSuccessful_ShouldCreateInvitedIdentityUserWithoutPasswordAndSendInvitationEmail()
    {
        var context = TestHelpers.CreateContext(tenantId: 77);
        using var harness = TestHelpers.CreateIdentityHarness();
        var service = TestHelpers.CreateAuthService(context, harness);

        context.Roles.Add(new Role { Id = 2, Name = "Accounting" });
        await context.SaveChangesAsync();

        var user = await service.InviteTenantUserAsync(new InviteTenantUserDTO
        {
            Email = "new.accountant@test.com",
            FirstName = "New",
            LastName = "Accountant",
            RoleName = "Accounting"
        });

        user.CompanyId.Should().Be(77);
        user.PasswordHash.Should().BeEmpty();
        user.PasswordSalt.Should().BeNull();
        user.FullName.Should().Be("New Accountant");
        user.IsActive.Should().BeFalse();
        user.Status.Should().Be(ApplicationUserStatuses.Invited);

        var identityUser = await harness.IdentityContext.Users.SingleAsync(u => u.LegacyUserId == user.Id);
        identityUser.CompanyId.Should().Be(77);
        identityUser.Email.Should().Be("new.accountant@test.com");
        (await harness.UserManager.GetRolesAsync(identityUser)).Should().ContainSingle("Accounting");
        identityUser.PasswordHash.Should().BeNullOrEmpty();
        identityUser.RequireEmailConfirmation.Should().BeTrue();
        identityUser.EmailConfirmed.Should().BeFalse();
        identityUser.IsActive.Should().BeFalse();
        identityUser.Status.Should().Be(ApplicationUserStatuses.Invited);
        harness.EmailService.SentInvitationEmails.Should().ContainSingle();
        harness.EmailService.SentInvitationEmails.Single().ConfirmationLink.Should().Contain("/confirm-email?");
    }

    [Fact]
    public async Task InviteTenantUserAsync_WhenRoleIsSuperAdmin_ShouldRejectTheRequest()
    {
        var context = TestHelpers.CreateContext(tenantId: 77);
        using var harness = TestHelpers.CreateIdentityHarness();
        var service = TestHelpers.CreateAuthService(context, harness);

        context.Roles.Add(new Role { Id = 9, Name = ApplicationRoles.SuperAdmin });
        await context.SaveChangesAsync();

        var act = async () => await service.InviteTenantUserAsync(new InviteTenantUserDTO
        {
            Email = "platform-admin@test.com",
            RoleName = ApplicationRoles.SuperAdmin
        });

        await act.Should().ThrowAsync<Exception>()
            .WithMessage($"{ApplicationRoles.SuperAdmin} role cannot be assigned from this endpoint.");
    }

    [Fact]
    public async Task ConfirmEmailAsync_WhenInvitedUserConfirms_ShouldReturnPasswordSetupRedirectAndKeepAccountInactive()
    {
        var context = TestHelpers.CreateContext(tenantId: 55);
        using var harness = TestHelpers.CreateIdentityHarness();
        var service = TestHelpers.CreateAuthService(context, harness);

        var role = new Role { Id = 2, Name = ApplicationRoles.Accounting };
        var company = new Company { Id = 55, Name = "Invite Co", IsActive = true, Status = ApplicationUserStatuses.Active };
        var user = new User
        {
            CompanyId = company.Id,
            Email = "invited.confirm@test.com",
            FullName = "Invited User",
            RoleId = role.Id,
            Role = role,
            PasswordHash = string.Empty,
            PasswordSalt = null,
            IsActive = false,
            Status = ApplicationUserStatuses.Invited
        };

        context.Roles.Add(role);
        context.Companies.Add(company);
        context.Users.Add(user);
        await context.SaveChangesAsync();

        await harness.AccountService.EnsureProvisionedWithoutPasswordAsync(
            TestHelpers.CreateIdentitySnapshot(user, role.Name, requireEmailConfirmation: true, emailConfirmed: false));

        var identityUser = await harness.IdentityContext.Users.SingleAsync(u => u.LegacyUserId == user.Id);
        var rawToken = await harness.UserManager.GenerateEmailConfirmationTokenAsync(identityUser);
        var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(rawToken));

        var result = await service.ConfirmEmailAsync(new ConfirmEmailDTO
        {
            Email = identityUser.Email!,
            Token = encodedToken
        });

        result.RequiresPasswordSetup.Should().BeTrue();
        result.RedirectPath.Should().Contain("/reset-password?");
        result.RedirectPath.Should().Contain("flow=invite");

        var refreshedIdentityUser = await harness.IdentityContext.Users.SingleAsync(u => u.LegacyUserId == user.Id);
        refreshedIdentityUser.EmailConfirmed.Should().BeTrue();
        refreshedIdentityUser.IsActive.Should().BeFalse();
        refreshedIdentityUser.Status.Should().Be(ApplicationUserStatuses.Invited);

        var refreshedLegacyUser = await context.Users.IgnoreQueryFilters().SingleAsync(u => u.Id == user.Id);
        refreshedLegacyUser.IsActive.Should().BeFalse();
        refreshedLegacyUser.Status.Should().Be(ApplicationUserStatuses.Invited);
    }

    [Fact]
    public async Task ResetPasswordAsync_WhenInvitedUserIsAlreadyConfirmed_ShouldActivateAccount()
    {
        var context = TestHelpers.CreateContext(tenantId: 56);
        using var harness = TestHelpers.CreateIdentityHarness();
        var service = TestHelpers.CreateAuthService(context, harness);

        var role = new Role { Id = 3, Name = ApplicationRoles.Management };
        var company = new Company { Id = 56, Name = "Invite Activation Co", IsActive = true, Status = ApplicationUserStatuses.Active };
        var user = new User
        {
            CompanyId = company.Id,
            Email = "setup.complete@test.com",
            FullName = "Setup Complete",
            RoleId = role.Id,
            Role = role,
            PasswordHash = string.Empty,
            PasswordSalt = null,
            IsActive = false,
            Status = ApplicationUserStatuses.Invited
        };

        context.Roles.Add(role);
        context.Companies.Add(company);
        context.Users.Add(user);
        await context.SaveChangesAsync();

        await harness.AccountService.EnsureProvisionedWithoutPasswordAsync(
            TestHelpers.CreateIdentitySnapshot(user, role.Name, requireEmailConfirmation: true, emailConfirmed: true));

        var identityUser = await harness.IdentityContext.Users.SingleAsync(u => u.LegacyUserId == user.Id);
        var rawToken = await harness.UserManager.GeneratePasswordResetTokenAsync(identityUser);
        var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(rawToken));

        await service.ResetPasswordAsync(new ResetPasswordDTO
        {
            Email = identityUser.Email!,
            Token = encodedToken,
            NewPassword = "BetterPassword456!",
            ConfirmPassword = "BetterPassword456!"
        });

        var refreshedIdentityUser = await harness.IdentityContext.Users.SingleAsync(u => u.LegacyUserId == user.Id);
        refreshedIdentityUser.IsActive.Should().BeTrue();
        refreshedIdentityUser.Status.Should().Be(ApplicationUserStatuses.Active);
        (await harness.UserManager.CheckPasswordAsync(refreshedIdentityUser, "BetterPassword456!")).Should().BeTrue();

        var refreshedLegacyUser = await context.Users.IgnoreQueryFilters().SingleAsync(u => u.Id == user.Id);
        refreshedLegacyUser.IsActive.Should().BeTrue();
        refreshedLegacyUser.Status.Should().Be(ApplicationUserStatuses.Active);

        var loginResponse = await service.LoginAsync(new LoginDTO
        {
            Email = user.Email,
            Password = "BetterPassword456!"
        });

        loginResponse.Token.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ResetPasswordAsync_WhenInvitedUserIsNotConfirmed_ShouldRemainInvitedAndInactive()
    {
        var context = TestHelpers.CreateContext(tenantId: 57);
        using var harness = TestHelpers.CreateIdentityHarness();
        var service = TestHelpers.CreateAuthService(context, harness);

        var role = new Role { Id = 4, Name = ApplicationRoles.Management };
        var company = new Company { Id = 57, Name = "Invite Pending Co", IsActive = true, Status = ApplicationUserStatuses.Active };
        var user = new User
        {
            CompanyId = company.Id,
            Email = "pending.setup@test.com",
            FullName = "Pending Setup",
            RoleId = role.Id,
            Role = role,
            PasswordHash = string.Empty,
            PasswordSalt = null,
            IsActive = false,
            Status = ApplicationUserStatuses.Invited
        };

        context.Roles.Add(role);
        context.Companies.Add(company);
        context.Users.Add(user);
        await context.SaveChangesAsync();

        await harness.AccountService.EnsureProvisionedWithoutPasswordAsync(
            TestHelpers.CreateIdentitySnapshot(user, role.Name, requireEmailConfirmation: true, emailConfirmed: false));

        var identityUser = await harness.IdentityContext.Users.SingleAsync(u => u.LegacyUserId == user.Id);
        var rawToken = await harness.UserManager.GeneratePasswordResetTokenAsync(identityUser);
        var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(rawToken));

        await service.ResetPasswordAsync(new ResetPasswordDTO
        {
            Email = identityUser.Email!,
            Token = encodedToken,
            NewPassword = "BetterPassword456!",
            ConfirmPassword = "BetterPassword456!"
        });

        var refreshedIdentityUser = await harness.IdentityContext.Users.SingleAsync(u => u.LegacyUserId == user.Id);
        refreshedIdentityUser.IsActive.Should().BeFalse();
        refreshedIdentityUser.Status.Should().Be(ApplicationUserStatuses.Invited);
        (await harness.UserManager.CheckPasswordAsync(refreshedIdentityUser, "BetterPassword456!")).Should().BeTrue();

        var refreshedLegacyUser = await context.Users.IgnoreQueryFilters().SingleAsync(u => u.Id == user.Id);
        refreshedLegacyUser.IsActive.Should().BeFalse();
        refreshedLegacyUser.Status.Should().Be(ApplicationUserStatuses.Invited);
    }

    [Fact]
    public async Task ConfirmEmailAsync_WhenInvitedUserAlreadyHasPassword_ShouldActivateAndAllowLogin()
    {
        var context = TestHelpers.CreateContext(tenantId: 58);
        using var harness = TestHelpers.CreateIdentityHarness();
        var service = TestHelpers.CreateAuthService(context, harness);

        var role = new Role { Id = 2, Name = ApplicationRoles.Accounting };
        var company = new Company { Id = 58, Name = "Invite Finalize Co", IsActive = true, Status = ApplicationUserStatuses.Active };
        var user = new User
        {
            CompanyId = company.Id,
            Email = "invite.finalize@test.com",
            FullName = "Invite Finalize",
            RoleId = role.Id,
            Role = role,
            PasswordHash = string.Empty,
            PasswordSalt = null,
            IsActive = false,
            Status = ApplicationUserStatuses.Invited
        };

        context.Roles.Add(role);
        context.Companies.Add(company);
        context.Users.Add(user);
        await context.SaveChangesAsync();

        await harness.AccountService.EnsureProvisionedAsync(
            TestHelpers.CreateIdentitySnapshot(user, role.Name, requireEmailConfirmation: true, emailConfirmed: false),
            "LongPassword123!");

        var identityUser = await harness.IdentityContext.Users.SingleAsync(u => u.LegacyUserId == user.Id);
        var rawToken = await harness.UserManager.GenerateEmailConfirmationTokenAsync(identityUser);
        var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(rawToken));

        var result = await service.ConfirmEmailAsync(new ConfirmEmailDTO
        {
            Email = identityUser.Email!,
            Token = encodedToken
        });

        result.RequiresPasswordSetup.Should().BeFalse();

        var refreshedLegacyUser = await context.Users.IgnoreQueryFilters().SingleAsync(u => u.Id == user.Id);
        refreshedLegacyUser.IsActive.Should().BeTrue();
        refreshedLegacyUser.Status.Should().Be(ApplicationUserStatuses.Active);

        var loginResponse = await service.LoginAsync(new LoginDTO
        {
            Email = user.Email,
            Password = "LongPassword123!"
        });

        loginResponse.Token.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ResendInviteAsync_WhenUserIsUnconfirmed_ShouldSendInvitationEmail()
    {
        var context = TestHelpers.CreateContext(tenantId: 59);
        using var harness = TestHelpers.CreateIdentityHarness();
        var service = TestHelpers.CreateAuthService(context, harness);

        var role = new Role { Id = 2, Name = ApplicationRoles.Accounting };
        var company = new Company { Id = 59, Name = "Invite Resend Co", IsActive = true, Status = ApplicationUserStatuses.Active };
        var user = new User
        {
            CompanyId = company.Id,
            Email = "invite.resend@test.com",
            FullName = "Invite Resend",
            RoleId = role.Id,
            Role = role,
            PasswordHash = string.Empty,
            PasswordSalt = null,
            IsActive = false,
            Status = ApplicationUserStatuses.Invited
        };

        context.Roles.Add(role);
        context.Companies.Add(company);
        context.Users.Add(user);
        await context.SaveChangesAsync();

        await harness.AccountService.EnsureProvisionedWithoutPasswordAsync(
            TestHelpers.CreateIdentitySnapshot(user, role.Name, requireEmailConfirmation: true, emailConfirmed: false));

        await service.ResendInviteAsync(user.Id, company.Id);

        harness.EmailService.SentInvitationEmails.Should().ContainSingle();
        harness.EmailService.SentPasswordSetupEmails.Should().BeEmpty();
    }

    [Fact]
    public async Task ResendInviteAsync_WhenUserHasConfirmedEmail_ShouldSendPasswordSetupEmail()
    {
        var context = TestHelpers.CreateContext(tenantId: 60);
        using var harness = TestHelpers.CreateIdentityHarness();
        var service = TestHelpers.CreateAuthService(context, harness);

        var role = new Role { Id = 4, Name = ApplicationRoles.Management };
        var company = new Company { Id = 60, Name = "Invite Setup Resend Co", IsActive = true, Status = ApplicationUserStatuses.Active };
        var user = new User
        {
            CompanyId = company.Id,
            Email = "invite.setup.resend@test.com",
            FullName = "Invite Setup Resend",
            RoleId = role.Id,
            Role = role,
            PasswordHash = string.Empty,
            PasswordSalt = null,
            IsActive = false,
            Status = ApplicationUserStatuses.Invited
        };

        context.Roles.Add(role);
        context.Companies.Add(company);
        context.Users.Add(user);
        await context.SaveChangesAsync();

        await harness.AccountService.EnsureProvisionedWithoutPasswordAsync(
            TestHelpers.CreateIdentitySnapshot(user, role.Name, requireEmailConfirmation: true, emailConfirmed: true));

        await service.ResendInviteAsync(user.Id, company.Id);

        harness.EmailService.SentInvitationEmails.Should().BeEmpty();
        harness.EmailService.SentPasswordSetupEmails.Should().ContainSingle();
        harness.EmailService.SentPasswordSetupEmails.Single().ResetLink.Should().Contain("flow=invite");
    }

    [Fact]
    public async Task ResendInviteAsync_WhenTenantDoesNotOwnTheUser_ShouldThrow()
    {
        var context = TestHelpers.CreateContext(tenantId: 61);
        using var harness = TestHelpers.CreateIdentityHarness();
        var service = TestHelpers.CreateAuthService(context, harness);

        var role = new Role { Id = 2, Name = ApplicationRoles.Accounting };
        var company = new Company { Id = 62, Name = "Foreign Invite Co", IsActive = true, Status = ApplicationUserStatuses.Active };
        var user = new User
        {
            CompanyId = company.Id,
            Email = "foreign.invite@test.com",
            FullName = "Foreign Invite",
            RoleId = role.Id,
            Role = role,
            PasswordHash = string.Empty,
            PasswordSalt = null,
            IsActive = false,
            Status = ApplicationUserStatuses.Invited
        };

        context.Roles.Add(role);
        context.Companies.Add(company);
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var act = async () => await service.ResendInviteAsync(user.Id, tenantId: 61);

        await act.Should().ThrowAsync<Exception>().WithMessage("User not found.");
    }

    [Fact]
    public async Task ChangePasswordAsync_WhenIdentityUserExists_ShouldUpdateIdentityPasswordAndClearLegacyPassword()
    {
        var context = TestHelpers.CreateContext(tenantId: 12);
        using var harness = TestHelpers.CreateIdentityHarness();
        var service = TestHelpers.CreateAuthService(context, harness);

        var role = new Role { Id = 1, Name = ApplicationRoles.TenantOwner };
        var company = new Company { Id = 12, Name = "Password Co", IsActive = true, Status = "Active" };
        var user = TestHelpers.CreateUser(role, company.Id, "password@test.com", "LongPassword123!");

        context.Roles.Add(role);
        context.Companies.Add(company);
        context.Users.Add(user);
        await context.SaveChangesAsync();

        await harness.AccountService.EnsureProvisionedAsync(
            TestHelpers.CreateIdentitySnapshot(user, role.Name, requireEmailConfirmation: true, emailConfirmed: true),
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

        var role = new Role { Id = 1, Name = ApplicationRoles.TenantOwner };
        var company = new Company { Id = 13, Name = "Profile Co", IsActive = true, Status = "Active" };
        var user = TestHelpers.CreateUser(role, company.Id, "profile@test.com", "LongPassword123!");

        context.Roles.Add(role);
        context.Companies.Add(company);
        context.Users.Add(user);
        await context.SaveChangesAsync();

        await harness.AccountService.EnsureProvisionedAsync(
            TestHelpers.CreateIdentitySnapshot(user, role.Name, requireEmailConfirmation: true, emailConfirmed: true),
            "LongPassword123!");

        await service.UpdateProfileAsync(user.Id, new UpdateProfileDTO
        {
            Email = "updated.profile@test.com",
            FullName = "Updated Profile"
        });

        var profile = await service.GetCurrentProfileAsync(user.Id);
        profile.Email.Should().Be("updated.profile@test.com");
        profile.FullName.Should().Be("Updated Profile");
        profile.Role.Should().Be(ApplicationRoles.TenantOwner);
        profile.CompanyName.Should().Be(company.Name);

        var identityUser = await harness.IdentityContext.Users.SingleAsync(u => u.LegacyUserId == user.Id);
        identityUser.Email.Should().Be("updated.profile@test.com");
        identityUser.UserName.Should().Be("updated.profile@test.com");
        identityUser.FullName.Should().Be("Updated Profile");
        identityUser.RequireEmailConfirmation.Should().BeTrue();
        identityUser.EmailConfirmed.Should().BeFalse();
        harness.EmailService.SentConfirmationEmails.Should().ContainSingle();
    }

    [Fact]
    public async Task Login_WhenUserRequiresEmailConfirmation_ShouldReturnGenericUnauthorizedUntilConfirmed()
    {
        var context = TestHelpers.CreateContext(tenantId: 15);
        using var harness = TestHelpers.CreateIdentityHarness();
        var service = TestHelpers.CreateAuthService(context, harness);

        var role = new Role { Id = 1, Name = ApplicationRoles.TenantOwner };
        var company = new Company { Id = 15, Name = "Confirm Co", IsActive = true, Status = "Active" };
        var user = TestHelpers.CreateUser(role, company.Id, "confirm@test.com", "LongPassword123!");

        context.Roles.Add(role);
        context.Companies.Add(company);
        context.Users.Add(user);
        await context.SaveChangesAsync();

        await harness.AccountService.EnsureProvisionedAsync(
            TestHelpers.CreateIdentitySnapshot(user, role.Name, requireEmailConfirmation: true, emailConfirmed: false),
            "LongPassword123!");

        var controller = new AuthController(service);

        var response = await controller.Login(new LoginDTO
        {
            Email = "confirm@test.com",
            Password = "LongPassword123!"
        });

        var unauthorized = response.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        TestHelpers.GetAnonymousStringValue(unauthorized.Value, "error").Should().Be("Invalid email or password. Please try again later.");
    }

    [Fact]
    public async Task Login_WhenUserIsUnconfirmedWithoutRequireFlag_ShouldStillReturnGenericUnauthorized()
    {
        var context = TestHelpers.CreateContext(tenantId: 115);
        using var harness = TestHelpers.CreateIdentityHarness();
        var service = TestHelpers.CreateAuthService(context, harness);

        var role = new Role { Id = 1, Name = ApplicationRoles.TenantOwner };
        var company = new Company { Id = 115, Name = "Blanket Confirm Co", IsActive = true, Status = "Active" };
        var user = TestHelpers.CreateUser(role, company.Id, "blanket@test.com", "LongPassword123!");

        context.Roles.Add(role);
        context.Companies.Add(company);
        context.Users.Add(user);
        await context.SaveChangesAsync();

        await harness.AccountService.EnsureProvisionedAsync(
            TestHelpers.CreateIdentitySnapshot(user, role.Name, requireEmailConfirmation: false, emailConfirmed: false),
            "LongPassword123!");

        var controller = new AuthController(service);

        var response = await controller.Login(new LoginDTO
        {
            Email = "blanket@test.com",
            Password = "LongPassword123!"
        });

        var unauthorized = response.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        TestHelpers.GetAnonymousStringValue(unauthorized.Value, "error").Should().Be("Invalid email or password. Please try again later.");
    }

    [Fact]
    public async Task Login_WhenSuperAdminEmailIsUnconfirmed_ShouldAllowLogin()
    {
        var context = TestHelpers.CreateContext(tenantId: 116);
        using var harness = TestHelpers.CreateIdentityHarness();
        var service = TestHelpers.CreateAuthService(context, harness);

        var role = new Role { Id = 9, Name = ApplicationRoles.SuperAdmin };
        var company = new Company { Id = 116, Name = "Ops HQ", IsActive = true, Status = "Active" };
        var user = TestHelpers.CreateUser(role, company.Id, "superadmin@test.com", "LongPassword123!");

        context.Roles.Add(role);
        context.Companies.Add(company);
        context.Users.Add(user);
        await context.SaveChangesAsync();

        await harness.AccountService.EnsureProvisionedAsync(
            TestHelpers.CreateIdentitySnapshot(user, role.Name, requireEmailConfirmation: false, emailConfirmed: false),
            "LongPassword123!");

        var loginResponse = await service.LoginAsync(new LoginDTO
        {
            Email = "superadmin@test.com",
            Password = "LongPassword123!"
        });

        loginResponse.Token.Should().NotBeNullOrWhiteSpace();
        loginResponse.Role.Should().Be(ApplicationRoles.SuperAdmin);
    }

    [Fact]
    public async Task ConfirmEmailAsync_WhenTokenIsValid_ShouldConfirmUserAndAllowLogin()
    {
        var context = TestHelpers.CreateContext(tenantId: 16);
        using var harness = TestHelpers.CreateIdentityHarness();
        var service = TestHelpers.CreateAuthService(context, harness);

        var role = new Role { Id = 1, Name = ApplicationRoles.TenantOwner };
        var company = new Company { Id = 16, Name = "Confirm Flow Co", IsActive = true, Status = "Active" };
        var user = TestHelpers.CreateUser(role, company.Id, "confirm-flow@test.com", "LongPassword123!");

        context.Roles.Add(role);
        context.Companies.Add(company);
        context.Users.Add(user);
        await context.SaveChangesAsync();

        await harness.AccountService.EnsureProvisionedAsync(
            TestHelpers.CreateIdentitySnapshot(user, role.Name, requireEmailConfirmation: true, emailConfirmed: false),
            "LongPassword123!");

        var identityUser = await harness.IdentityContext.Users.SingleAsync(u => u.LegacyUserId == user.Id);
        var rawToken = await harness.UserManager.GenerateEmailConfirmationTokenAsync(identityUser);
        var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(rawToken));

        await service.ConfirmEmailAsync(new ConfirmEmailDTO
        {
            Email = identityUser.Email!,
            Token = encodedToken
        });

        var refreshedUser = await harness.IdentityContext.Users.SingleAsync(u => u.LegacyUserId == user.Id);
        refreshedUser.EmailConfirmed.Should().BeTrue();

        var loginResponse = await service.LoginAsync(new LoginDTO
        {
            Email = identityUser.Email!,
            Password = "LongPassword123!"
        });

        loginResponse.Token.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ResendConfirmationAsync_WhenAccountRequiresConfirmation_ShouldSendFreshConfirmationEmail()
    {
        var context = TestHelpers.CreateContext(tenantId: 17);
        using var harness = TestHelpers.CreateIdentityHarness();
        var service = TestHelpers.CreateAuthService(context, harness);

        var role = new Role { Id = 2, Name = "Accounting" };
        var company = new Company { Id = 17, Name = "Resend Co", IsActive = true, Status = "Active" };
        var user = TestHelpers.CreateUser(role, company.Id, "resend@test.com", "LongPassword123!");

        context.Roles.Add(role);
        context.Companies.Add(company);
        context.Users.Add(user);
        await context.SaveChangesAsync();

        await harness.AccountService.EnsureProvisionedAsync(
            TestHelpers.CreateIdentitySnapshot(user, role.Name, requireEmailConfirmation: true, emailConfirmed: false),
            "LongPassword123!");

        await service.ResendConfirmationAsync(new ResendConfirmationDTO { Email = user.Email });

        harness.EmailService.SentConfirmationEmails.Should().ContainSingle();
        harness.EmailService.SentConfirmationEmails.Single().ConfirmationLink.Should().Contain("/confirm-email?");
    }

    [Fact]
    public async Task ResendConfirmationAsync_WhenLegacyOnlyAccountExists_ShouldProvisionUnconfirmedIdentityAndSendFreshConfirmationEmail()
    {
        var context = TestHelpers.CreateContext(tenantId: 117);
        using var harness = TestHelpers.CreateIdentityHarness();
        var service = TestHelpers.CreateAuthService(context, harness);

        var role = new Role { Id = 4, Name = "Management" };
        var company = new Company { Id = 117, Name = "Legacy Resend Co", IsActive = true, Status = "Active" };
        var user = TestHelpers.CreateUser(role, company.Id, "legacy-resend@test.com", "LongPassword123!");

        context.Roles.Add(role);
        context.Companies.Add(company);
        context.Users.Add(user);
        await context.SaveChangesAsync();

        await service.ResendConfirmationAsync(new ResendConfirmationDTO { Email = user.Email });

        var identityUser = await harness.IdentityContext.Users.SingleAsync(u => u.LegacyUserId == user.Id);
        identityUser.RequireEmailConfirmation.Should().BeTrue();
        identityUser.EmailConfirmed.Should().BeFalse();
        harness.EmailService.SentConfirmationEmails.Should().ContainSingle();
    }

    [Fact]
    public async Task ForgotAndResetPassword_WhenLegacyOnlyAccountExists_ShouldProvisionIdentityWithoutAutoConfirmingEmail()
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

        harness.EmailService.SentResetEmails.Should().ContainSingle();
        var sentEmail = harness.EmailService.SentResetEmails.Single();
        sentEmail.Email.Should().Be("reset@test.com");
        sentEmail.ResetLink.Should().Contain("/reset-password?");

        var query = QueryHelpers.ParseQuery(new Uri(sentEmail.ResetLink).Query);
        var encodedToken = query["token"].ToString();
        var encodedEmail = query["email"].ToString();
        encodedToken.Should().NotBeNullOrWhiteSpace();
        encodedEmail.Should().NotBeNullOrWhiteSpace();

        var provisionedIdentityUser = await harness.IdentityContext.Users.SingleAsync(u => u.LegacyUserId == user.Id);
        provisionedIdentityUser.RequireEmailConfirmation.Should().BeTrue();
        provisionedIdentityUser.EmailConfirmed.Should().BeFalse();

        await service.ResetPasswordAsync(new ResetPasswordDTO
        {
            Email = encodedEmail,
            Token = encodedToken,
            NewPassword = "BetterPassword456!",
            ConfirmPassword = "BetterPassword456!"
        });

        var identityUser = await harness.IdentityContext.Users.SingleAsync(u => u.LegacyUserId == user.Id);
        (await harness.UserManager.CheckPasswordAsync(identityUser, "BetterPassword456!")).Should().BeTrue();
        identityUser.EmailConfirmed.Should().BeFalse();

        var reloadedUser = await context.Users.IgnoreQueryFilters().SingleAsync(u => u.Id == user.Id);
        reloadedUser.PasswordHash.Should().BeEmpty();
        reloadedUser.PasswordSalt.Should().BeNull();

        var act = async () => await service.LoginAsync(new LoginDTO
        {
            Email = user.Email,
            Password = "BetterPassword456!"
        });

        var exception = await Record.ExceptionAsync(act);
        exception.Should().NotBeNull();
        exception!.Message.Should().Be("Invalid email or password. Please try again later.");
    }

    [Fact]
    public async Task SendPasswordResetAsync_WhenDevelopmentOriginDiffersFromConfiguredClientBaseUrl_ShouldUseRequestOriginForResetLink()
    {
        var context = TestHelpers.CreateContext(tenantId: 140);
        using var harness = TestHelpers.CreateIdentityHarness();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JwtSettings:Secret"] = "super-secret-key-for-tests-only-1234567890",
                ["JwtSettings:Issuer"] = "issuer",
                ["JwtSettings:Audience"] = "audience",
                ["JwtSettings:ExpiryMinutes"] = "60",
                ["JwtSettings:ClockSkewSeconds"] = "60",
                ["AuthSecurity:Lockout:MaxFailedAccessAttempts"] = "5",
                ["AuthSecurity:Lockout:LockoutMinutes"] = "15",
                ["IdentityTokens:PasswordResetTokenLifespanMinutes"] = "120",
                ["IdentityTokens:EmailConfirmationTokenLifespanMinutes"] = "1440",
                ["AppUrls:ClientBaseUrl"] = "https://localhost:5173"
            })
            .Build();

        var httpContextAccessor = TestHelpers.CreateHttpContextAccessor(
            scheme: "https",
            host: "localhost:7273",
            origin: "https://localhost:7273");

        var service = TestHelpers.CreateAuthService(
            context,
            harness,
            configuration: configuration,
            httpContextAccessor: httpContextAccessor);

        var role = new Role { Id = 3, Name = "Management" };
        var company = new Company { Id = 140, Name = "Reset Origin Co", IsActive = true, Status = "Active" };
        var user = TestHelpers.CreateUser(role, company.Id, "reset-origin@test.com", "LongPassword123!");

        context.Roles.Add(role);
        context.Companies.Add(company);
        context.Users.Add(user);
        await context.SaveChangesAsync();

        await service.SendPasswordResetAsync(new ForgotPasswordDTO { Email = user.Email });

        harness.EmailService.SentResetEmails.Should().ContainSingle();
        harness.EmailService.SentResetEmails.Single().ResetLink
            .Should().StartWith("https://localhost:7273/reset-password?");
    }

    [Fact]
    public async Task BeginAuthenticatorSetupAsync_WhenCalled_ShouldReturnSharedKeyAndGoogleAuthenticatorUri()
    {
        var context = TestHelpers.CreateContext(tenantId: 141);
        using var harness = TestHelpers.CreateIdentityHarness();
        var service = TestHelpers.CreateAuthService(context, harness);

        var role = new Role { Id = 1, Name = ApplicationRoles.TenantOwner };
        var company = new Company { Id = 141, Name = "Mfa Setup Co", IsActive = true, Status = "Active" };
        var user = TestHelpers.CreateUser(role, company.Id, "mfa-setup@test.com", "LongPassword123!");

        context.Roles.Add(role);
        context.Companies.Add(company);
        context.Users.Add(user);
        await context.SaveChangesAsync();

        await harness.AccountService.EnsureProvisionedAsync(
            TestHelpers.CreateIdentitySnapshot(user, role.Name, requireEmailConfirmation: true, emailConfirmed: true),
            "LongPassword123!");

        var setup = await service.BeginAuthenticatorSetupAsync(user.Id);

        setup.SharedKey.Should().NotBeNullOrWhiteSpace();
        setup.AuthenticatorUri.Should().StartWith("otpauth://totp/");
        setup.AuthenticatorUri.Should().Contain("issuer=AccountingSystem");
        setup.AuthenticatorUri.Should().Contain(Uri.EscapeDataString(user.Email));
        setup.IsTwoFactorEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task LoginAsync_WhenMfaIsEnabled_ShouldReturnChallengeInsteadOfJwt()
    {
        var context = TestHelpers.CreateContext(tenantId: 142);
        using var harness = TestHelpers.CreateIdentityHarness();
        var service = TestHelpers.CreateAuthService(context, harness);

        var role = new Role { Id = 1, Name = ApplicationRoles.TenantOwner };
        var company = new Company { Id = 142, Name = "Mfa Login Co", IsActive = true, Status = "Active" };
        var user = TestHelpers.CreateUser(role, company.Id, "mfa-login@test.com", "LongPassword123!");

        context.Roles.Add(role);
        context.Companies.Add(company);
        context.Users.Add(user);
        await context.SaveChangesAsync();

        await harness.AccountService.EnsureProvisionedAsync(
            TestHelpers.CreateIdentitySnapshot(user, role.Name, requireEmailConfirmation: true, emailConfirmed: true),
            "LongPassword123!");

        var setup = await service.BeginAuthenticatorSetupAsync(user.Id);
        var setupCode = TestHelpers.GenerateAuthenticatorCode(setup.SharedKey);
        var recoveryCodes = await service.VerifyAuthenticatorSetupAsync(user.Id, new VerifyAuthenticatorSetupDTO { Code = setupCode });
        recoveryCodes.RecoveryCodes.Should().HaveCount(10);

        var response = await service.LoginAsync(new LoginDTO
        {
            Email = user.Email,
            Password = "LongPassword123!"
        });

        response.RequiresTwoFactor.Should().BeTrue();
        response.TwoFactorChallengeToken.Should().NotBeNullOrWhiteSpace();
        response.Token.Should().BeEmpty();
        response.Message.Should().Contain("Google Authenticator");
    }

    [Fact]
    public async Task CompleteMfaLoginAsync_WhenAuthenticatorCodeIsValid_ShouldReturnJwt()
    {
        var context = TestHelpers.CreateContext(tenantId: 143);
        using var harness = TestHelpers.CreateIdentityHarness();
        var service = TestHelpers.CreateAuthService(context, harness);

        var role = new Role { Id = 1, Name = ApplicationRoles.TenantOwner };
        var company = new Company { Id = 143, Name = "Mfa Verify Co", IsActive = true, Status = "Active" };
        var user = TestHelpers.CreateUser(role, company.Id, "mfa-verify@test.com", "LongPassword123!");

        context.Roles.Add(role);
        context.Companies.Add(company);
        context.Users.Add(user);
        await context.SaveChangesAsync();

        await harness.AccountService.EnsureProvisionedAsync(
            TestHelpers.CreateIdentitySnapshot(user, role.Name, requireEmailConfirmation: true, emailConfirmed: true),
            "LongPassword123!");

        var setup = await service.BeginAuthenticatorSetupAsync(user.Id);
        await service.VerifyAuthenticatorSetupAsync(user.Id, new VerifyAuthenticatorSetupDTO
        {
            Code = TestHelpers.GenerateAuthenticatorCode(setup.SharedKey)
        });

        var loginResponse = await service.LoginAsync(new LoginDTO
        {
            Email = user.Email,
            Password = "LongPassword123!"
        });

        var mfaResponse = await service.CompleteMfaLoginAsync(new LoginMfaDTO
        {
            ChallengeToken = loginResponse.TwoFactorChallengeToken,
            TwoFactorCode = TestHelpers.GenerateAuthenticatorCode(setup.SharedKey)
        });

        mfaResponse.RequiresTwoFactor.Should().BeFalse();
        mfaResponse.Token.Should().NotBeNullOrWhiteSpace();
        mfaResponse.CompanyId.Should().Be(company.Id);
        mfaResponse.Role.Should().Be(ApplicationRoles.TenantOwner);
    }

    [Fact]
    public async Task CompleteMfaLoginAsync_WhenSecurityStampChangesAfterChallenge_ShouldStillReturnJwt()
    {
        var context = TestHelpers.CreateContext(tenantId: 1431);
        using var harness = TestHelpers.CreateIdentityHarness();
        var service = TestHelpers.CreateAuthService(context, harness);

        var role = new Role { Id = 1, Name = ApplicationRoles.TenantOwner };
        var company = new Company { Id = 1431, Name = "Mfa Stamp Co", IsActive = true, Status = "Active" };
        var user = TestHelpers.CreateUser(role, company.Id, "mfa-stamp@test.com", "LongPassword123!");

        context.Roles.Add(role);
        context.Companies.Add(company);
        context.Users.Add(user);
        await context.SaveChangesAsync();

        await harness.AccountService.EnsureProvisionedAsync(
            TestHelpers.CreateIdentitySnapshot(user, role.Name, requireEmailConfirmation: true, emailConfirmed: true),
            "LongPassword123!");

        var setup = await service.BeginAuthenticatorSetupAsync(user.Id);
        await service.VerifyAuthenticatorSetupAsync(user.Id, new VerifyAuthenticatorSetupDTO
        {
            Code = TestHelpers.GenerateAuthenticatorCode(setup.SharedKey)
        });

        var loginResponse = await service.LoginAsync(new LoginDTO
        {
            Email = user.Email,
            Password = "LongPassword123!"
        });

        var identityUser = await harness.IdentityContext.Users.SingleAsync(u => u.LegacyUserId == user.Id);
        identityUser.SecurityStamp = Guid.NewGuid().ToString("N");
        await harness.IdentityContext.SaveChangesAsync();

        var mfaResponse = await service.CompleteMfaLoginAsync(new LoginMfaDTO
        {
            ChallengeToken = loginResponse.TwoFactorChallengeToken,
            TwoFactorCode = TestHelpers.GenerateAuthenticatorCode(setup.SharedKey)
        });

        mfaResponse.Token.Should().NotBeNullOrWhiteSpace();
        mfaResponse.Role.Should().Be(ApplicationRoles.TenantOwner);
    }

    [Fact]
    public async Task CompleteMfaLoginAsync_WhenRecoveryCodeIsUsed_ShouldRejectReuse()
    {
        var context = TestHelpers.CreateContext(tenantId: 144);
        using var harness = TestHelpers.CreateIdentityHarness();
        var service = TestHelpers.CreateAuthService(context, harness);

        var role = new Role { Id = 2, Name = "Accounting" };
        var company = new Company { Id = 144, Name = "Recovery Co", IsActive = true, Status = "Active" };
        var user = TestHelpers.CreateUser(role, company.Id, "mfa-recovery@test.com", "LongPassword123!");

        context.Roles.Add(role);
        context.Companies.Add(company);
        context.Users.Add(user);
        await context.SaveChangesAsync();

        await harness.AccountService.EnsureProvisionedAsync(
            TestHelpers.CreateIdentitySnapshot(user, role.Name, requireEmailConfirmation: true, emailConfirmed: true),
            "LongPassword123!");

        var setup = await service.BeginAuthenticatorSetupAsync(user.Id);
        var recoveryCodes = await service.VerifyAuthenticatorSetupAsync(user.Id, new VerifyAuthenticatorSetupDTO
        {
            Code = TestHelpers.GenerateAuthenticatorCode(setup.SharedKey)
        });

        var recoveryCode = recoveryCodes.RecoveryCodes.First();
        var loginResponse = await service.LoginAsync(new LoginDTO
        {
            Email = user.Email,
            Password = "LongPassword123!"
        });

        var successResponse = await service.CompleteMfaLoginAsync(new LoginMfaDTO
        {
            ChallengeToken = loginResponse.TwoFactorChallengeToken,
            RecoveryCode = recoveryCode
        });

        successResponse.Token.Should().NotBeNullOrWhiteSpace();

        var secondChallenge = await service.LoginAsync(new LoginDTO
        {
            Email = user.Email,
            Password = "LongPassword123!"
        });

        var act = async () => await service.CompleteMfaLoginAsync(new LoginMfaDTO
        {
            ChallengeToken = secondChallenge.TwoFactorChallengeToken,
            RecoveryCode = recoveryCode
        });

        var exception = await Record.ExceptionAsync(act);
        exception.Should().NotBeNull();
        exception!.Message.Should().Be("The recovery code is invalid. Please try again.");
    }

    [Fact]
    public async Task CompleteMfaLoginAsync_WhenRecoveryCodeContainsWhitespace_ShouldAcceptIt()
    {
        var context = TestHelpers.CreateContext(tenantId: 1441);
        using var harness = TestHelpers.CreateIdentityHarness();
        var service = TestHelpers.CreateAuthService(context, harness);

        var role = new Role { Id = 2, Name = "Accounting" };
        var company = new Company { Id = 1441, Name = "Recovery Format Co", IsActive = true, Status = "Active" };
        var user = TestHelpers.CreateUser(role, company.Id, "mfa-recovery-format@test.com", "LongPassword123!");

        context.Roles.Add(role);
        context.Companies.Add(company);
        context.Users.Add(user);
        await context.SaveChangesAsync();

        await harness.AccountService.EnsureProvisionedAsync(
            TestHelpers.CreateIdentitySnapshot(user, role.Name, requireEmailConfirmation: true, emailConfirmed: true),
            "LongPassword123!");

        var setup = await service.BeginAuthenticatorSetupAsync(user.Id);
        var recoveryCodes = await service.VerifyAuthenticatorSetupAsync(user.Id, new VerifyAuthenticatorSetupDTO
        {
            Code = TestHelpers.GenerateAuthenticatorCode(setup.SharedKey)
        });

        var recoveryCode = recoveryCodes.RecoveryCodes.First();
        var formattedRecoveryCode = $"  {recoveryCode}  ";
        var loginResponse = await service.LoginAsync(new LoginDTO
        {
            Email = user.Email,
            Password = "LongPassword123!"
        });

        var mfaResponse = await service.CompleteMfaLoginAsync(new LoginMfaDTO
        {
            ChallengeToken = loginResponse.TwoFactorChallengeToken,
            RecoveryCode = formattedRecoveryCode
        });

        mfaResponse.Token.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task CompleteMfaLoginAsync_WhenLegacyLinkChangesAfterChallenge_ShouldFailWithInvalidSession()
    {
        var context = TestHelpers.CreateContext(tenantId: 1442);
        using var harness = TestHelpers.CreateIdentityHarness();
        var service = TestHelpers.CreateAuthService(context, harness);

        var role = new Role { Id = 1, Name = ApplicationRoles.TenantOwner };
        var company = new Company { Id = 1442, Name = "Mismatch Co", IsActive = true, Status = "Active" };
        var user = TestHelpers.CreateUser(role, company.Id, "mfa-mismatch@test.com", "LongPassword123!");

        context.Roles.Add(role);
        context.Companies.Add(company);
        context.Users.Add(user);
        await context.SaveChangesAsync();

        await harness.AccountService.EnsureProvisionedAsync(
            TestHelpers.CreateIdentitySnapshot(user, role.Name, requireEmailConfirmation: true, emailConfirmed: true),
            "LongPassword123!");

        var setup = await service.BeginAuthenticatorSetupAsync(user.Id);
        await service.VerifyAuthenticatorSetupAsync(user.Id, new VerifyAuthenticatorSetupDTO
        {
            Code = TestHelpers.GenerateAuthenticatorCode(setup.SharedKey)
        });

        var loginResponse = await service.LoginAsync(new LoginDTO
        {
            Email = user.Email,
            Password = "LongPassword123!"
        });

        var identityUser = await harness.IdentityContext.Users.SingleAsync(u => u.LegacyUserId == user.Id);
        identityUser.LegacyUserId = user.Id + 9000;
        await harness.IdentityContext.SaveChangesAsync();

        var act = async () => await service.CompleteMfaLoginAsync(new LoginMfaDTO
        {
            ChallengeToken = loginResponse.TwoFactorChallengeToken,
            TwoFactorCode = TestHelpers.GenerateAuthenticatorCode(setup.SharedKey)
        });

        var exception = await Record.ExceptionAsync(act);
        exception.Should().NotBeNull();
        exception!.Message.Should().Be("The sign-in verification session is invalid or expired. Please sign in again.");
    }

    [Fact]
    public async Task CompleteMfaLoginAsync_WhenMfaIsDisabledAfterChallenge_ShouldFailWithInvalidSession()
    {
        var context = TestHelpers.CreateContext(tenantId: 1443);
        using var harness = TestHelpers.CreateIdentityHarness();
        var service = TestHelpers.CreateAuthService(context, harness);

        var role = new Role { Id = 1, Name = ApplicationRoles.TenantOwner };
        var company = new Company { Id = 1443, Name = "Disabled Challenge Co", IsActive = true, Status = "Active" };
        var user = TestHelpers.CreateUser(role, company.Id, "mfa-disabled-after-challenge@test.com", "LongPassword123!");

        context.Roles.Add(role);
        context.Companies.Add(company);
        context.Users.Add(user);
        await context.SaveChangesAsync();

        await harness.AccountService.EnsureProvisionedAsync(
            TestHelpers.CreateIdentitySnapshot(user, role.Name, requireEmailConfirmation: true, emailConfirmed: true),
            "LongPassword123!");

        var setup = await service.BeginAuthenticatorSetupAsync(user.Id);
        await service.VerifyAuthenticatorSetupAsync(user.Id, new VerifyAuthenticatorSetupDTO
        {
            Code = TestHelpers.GenerateAuthenticatorCode(setup.SharedKey)
        });

        var loginResponse = await service.LoginAsync(new LoginDTO
        {
            Email = user.Email,
            Password = "LongPassword123!"
        });

        await service.DisableMfaAsync(user.Id, new MfaReauthenticationDTO
        {
            CurrentPassword = "LongPassword123!"
        });

        var act = async () => await service.CompleteMfaLoginAsync(new LoginMfaDTO
        {
            ChallengeToken = loginResponse.TwoFactorChallengeToken,
            TwoFactorCode = TestHelpers.GenerateAuthenticatorCode(setup.SharedKey)
        });

        var exception = await Record.ExceptionAsync(act);
        exception.Should().NotBeNull();
        exception!.Message.Should().Be("The sign-in verification session is invalid or expired. Please sign in again.");
    }

    [Fact]
    public async Task CompleteMfaLoginAsync_WhenChallengeTokenIsTampered_ShouldFailWithInvalidSession()
    {
        var context = TestHelpers.CreateContext(tenantId: 1444);
        using var harness = TestHelpers.CreateIdentityHarness();
        var service = TestHelpers.CreateAuthService(context, harness);

        var role = new Role { Id = 1, Name = ApplicationRoles.TenantOwner };
        var company = new Company { Id = 1444, Name = "Tampered Challenge Co", IsActive = true, Status = "Active" };
        var user = TestHelpers.CreateUser(role, company.Id, "mfa-tampered@test.com", "LongPassword123!");

        context.Roles.Add(role);
        context.Companies.Add(company);
        context.Users.Add(user);
        await context.SaveChangesAsync();

        await harness.AccountService.EnsureProvisionedAsync(
            TestHelpers.CreateIdentitySnapshot(user, role.Name, requireEmailConfirmation: true, emailConfirmed: true),
            "LongPassword123!");

        var setup = await service.BeginAuthenticatorSetupAsync(user.Id);
        await service.VerifyAuthenticatorSetupAsync(user.Id, new VerifyAuthenticatorSetupDTO
        {
            Code = TestHelpers.GenerateAuthenticatorCode(setup.SharedKey)
        });

        var loginResponse = await service.LoginAsync(new LoginDTO
        {
            Email = user.Email,
            Password = "LongPassword123!"
        });

        var act = async () => await service.CompleteMfaLoginAsync(new LoginMfaDTO
        {
            ChallengeToken = $"{loginResponse.TwoFactorChallengeToken}tampered",
            TwoFactorCode = TestHelpers.GenerateAuthenticatorCode(setup.SharedKey)
        });

        var exception = await Record.ExceptionAsync(act);
        exception.Should().NotBeNull();
        exception!.Message.Should().Be("The sign-in verification session is invalid or expired. Please sign in again.");
    }

    [Fact]
    public async Task DisableMfaAsync_WhenCurrentPasswordIsValid_ShouldDisableTwoFactorAndRestorePasswordOnlyLogin()
    {
        var context = TestHelpers.CreateContext(tenantId: 145);
        using var harness = TestHelpers.CreateIdentityHarness();
        var service = TestHelpers.CreateAuthService(context, harness);

        var role = new Role { Id = 1, Name = ApplicationRoles.TenantOwner };
        var company = new Company { Id = 145, Name = "Disable Mfa Co", IsActive = true, Status = "Active" };
        var user = TestHelpers.CreateUser(role, company.Id, "disable-mfa@test.com", "LongPassword123!");

        context.Roles.Add(role);
        context.Companies.Add(company);
        context.Users.Add(user);
        await context.SaveChangesAsync();

        await harness.AccountService.EnsureProvisionedAsync(
            TestHelpers.CreateIdentitySnapshot(user, role.Name, requireEmailConfirmation: true, emailConfirmed: true),
            "LongPassword123!");

        var setup = await service.BeginAuthenticatorSetupAsync(user.Id);
        await service.VerifyAuthenticatorSetupAsync(user.Id, new VerifyAuthenticatorSetupDTO
        {
            Code = TestHelpers.GenerateAuthenticatorCode(setup.SharedKey)
        });

        await service.DisableMfaAsync(user.Id, new MfaReauthenticationDTO
        {
            CurrentPassword = "LongPassword123!"
        });

        var status = await service.GetMfaStatusAsync(user.Id);
        status.IsTwoFactorEnabled.Should().BeFalse();

        var loginResponse = await service.LoginAsync(new LoginDTO
        {
            Email = user.Email,
            Password = "LongPassword123!"
        });

        loginResponse.RequiresTwoFactor.Should().BeFalse();
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

        var role = new Role { Id = 1, Name = ApplicationRoles.TenantOwner };
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
                    TestHelpers.CreateIdentitySnapshot(user, role.Name, requireEmailConfirmation: true, emailConfirmed: true),
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
                ["IdentityTokens:PasswordResetTokenLifespanMinutes"] = "120",
                ["IdentityTokens:EmailConfirmationTokenLifespanMinutes"] = "1440",
                ["Mfa:AuthenticatorIssuer"] = "AccountingSystem",
                ["Mfa:LoginChallengeLifespanMinutes"] = "5",
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

    internal static LegacyIdentityUserSnapshot CreateIdentitySnapshot(
        User user,
        string roleName,
        bool? requireEmailConfirmation = null,
        bool? emailConfirmed = null) =>
        new(
            user.Id,
            user.CompanyId,
            user.Email,
            user.FullName ?? user.Email,
            user.Status,
            user.IsActive,
            user.IsDeleted,
            roleName,
            requireEmailConfirmation,
            emailConfirmed);

    internal static AuthService CreateAuthService(
        AccountingDbContext context,
        IdentityTestHarness harness,
        IConfiguration? configuration = null,
        Mock<ICaptchaService>? captcha = null,
        Mock<IAuthSecurityAuditService>? auditService = null,
        IHttpContextAccessor? httpContextAccessor = null,
        IWebHostEnvironment? environment = null)
    {
        configuration ??= CreateConfiguration();
        captcha ??= new Mock<ICaptchaService>();
        captcha.Setup(x => x.VerifyTokenAsync(It.IsAny<string>())).ReturnsAsync(true);
        httpContextAccessor ??= new HttpContextAccessor();
        environment ??= Mock.Of<IWebHostEnvironment>(x => x.EnvironmentName == Environments.Development);

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

        var mfaSettings = Microsoft.Extensions.Options.Options.Create(new MfaSettings
        {
            AuthenticatorIssuer = "AccountingSystem",
            LoginChallengeLifespanMinutes = 5
        });
        var loginChallengeTokenService = new LoginChallengeTokenService(configuration, mfaSettings);
        var mfaService = new MfaService(
            harness.UserManager,
            harness.AccountService,
            loginChallengeTokenService,
            auditService.Object,
            mfaSettings);

        return new AuthService(
            context,
            harness.IdentityContext,
            configuration,
            httpContextAccessor,
            environment,
            captcha.Object,
            Mock.Of<ILogger<AuthService>>(),
            auditService.Object,
            new LegacyPasswordService(),
            new JwtAuthTokenFactory(configuration),
            harness.AccountService,
            mfaService,
            loginChallengeTokenService,
            harness.EmailService,
            harness.UserManager);
    }

    internal static IHttpContextAccessor CreateHttpContextAccessor(string scheme, string host, string? origin = null, string? referer = null)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Scheme = scheme;
        httpContext.Request.Host = new HostString(host);

        if (!string.IsNullOrWhiteSpace(origin))
        {
            httpContext.Request.Headers.Origin = origin;
        }

        if (!string.IsNullOrWhiteSpace(referer))
        {
            httpContext.Request.Headers.Referer = referer;
        }

        return new HttpContextAccessor
        {
            HttpContext = httpContext
        };
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
                new Claim(ClaimTypes.Role, ApplicationRoles.TenantOwner),
                new Claim("UserId", "123"),
                new Claim("role", ApplicationRoles.TenantOwner),
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

    internal static string GenerateAuthenticatorCode(string sharedKey, DateTimeOffset? timestamp = null)
    {
        var secretBytes = DecodeBase32(sharedKey);
        var unixTime = (timestamp ?? DateTimeOffset.UtcNow).ToUnixTimeSeconds();
        var counter = unixTime / 30;

        Span<byte> counterBytes = stackalloc byte[8];
        BinaryPrimitives.WriteInt64BigEndian(counterBytes, counter);

        using var hmac = new HMACSHA1(secretBytes);
        var hash = hmac.ComputeHash(counterBytes.ToArray());
        var offset = hash[^1] & 0x0F;
        var binaryCode =
            ((hash[offset] & 0x7F) << 24) |
            ((hash[offset + 1] & 0xFF) << 16) |
            ((hash[offset + 2] & 0xFF) << 8) |
            (hash[offset + 3] & 0xFF);

        var code = binaryCode % 1_000_000;
        return code.ToString("D6");
    }

    internal static string? GetAnonymousStringValue(object? source, string propertyName)
    {
        return source?.GetType().GetProperty(propertyName)?.GetValue(source)?.ToString();
    }

    internal static IdentityTestHarness CreateIdentityHarness()
    {
        return new IdentityTestHarness();
    }

    private static byte[] DecodeBase32(string value)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var cleanedValue = value
            .Trim()
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .TrimEnd('=')
            .ToUpperInvariant();

        var output = new List<byte>();
        var bitBuffer = 0;
        var bitsInBuffer = 0;

        foreach (var character in cleanedValue)
        {
            var index = alphabet.IndexOf(character);
            if (index < 0)
            {
                throw new InvalidOperationException("Invalid Base32 character in authenticator key.");
            }

            bitBuffer = (bitBuffer << 5) | index;
            bitsInBuffer += 5;

            if (bitsInBuffer >= 8)
            {
                bitsInBuffer -= 8;
                output.Add((byte)((bitBuffer >> bitsInBuffer) & 0xFF));
            }
        }

        return output.ToArray();
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
        services.AddHttpContextAccessor();

        services.AddDbContext<IdentityAuthDbContext>(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services.Configure<PasswordResetTokenProviderOptions>(options =>
        {
            options.TokenLifespan = TimeSpan.FromHours(2);
        });
        services.Configure<EmailConfirmationTokenProviderOptions>(options =>
        {
            options.TokenLifespan = TimeSpan.FromHours(24);
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
                options.Tokens.PasswordResetTokenProvider = "AccSysPasswordReset";
                options.Tokens.EmailConfirmationTokenProvider = "AccSysEmailConfirmation";
            })
            .AddRoles<ApplicationRole>()
            .AddEntityFrameworkStores<IdentityAuthDbContext>()
            .AddTokenProvider<PasswordResetTokenProvider<ApplicationUser>>("AccSysPasswordReset")
            .AddTokenProvider<EmailConfirmationTokenProvider<ApplicationUser>>("AccSysEmailConfirmation")
            .AddDefaultTokenProviders()
            .AddSignInManager();

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
    public List<SentResetEmail> SentResetEmails { get; } = new();
    public List<SentConfirmationEmail> SentConfirmationEmails { get; } = new();
    public List<SentInvitationEmail> SentInvitationEmails { get; } = new();
    public List<SentPasswordSetupEmail> SentPasswordSetupEmails { get; } = new();

    public Task SendPasswordResetAsync(string email, string fullName, string resetLink, CancellationToken cancellationToken = default)
    {
        SentResetEmails.Add(new SentResetEmail(email, fullName, resetLink));
        return Task.CompletedTask;
    }

    public Task SendEmailConfirmationAsync(string email, string fullName, string confirmationLink, CancellationToken cancellationToken = default)
    {
        SentConfirmationEmails.Add(new SentConfirmationEmail(email, fullName, confirmationLink));
        return Task.CompletedTask;
    }

    public Task SendTenantInvitationAsync(string email, string fullName, string confirmationLink, CancellationToken cancellationToken = default)
    {
        SentInvitationEmails.Add(new SentInvitationEmail(email, fullName, confirmationLink));
        return Task.CompletedTask;
    }

    public Task SendTenantPasswordSetupAsync(string email, string fullName, string resetLink, CancellationToken cancellationToken = default)
    {
        SentPasswordSetupEmails.Add(new SentPasswordSetupEmail(email, fullName, resetLink));
        return Task.CompletedTask;
    }
}

internal sealed record SentResetEmail(string Email, string FullName, string ResetLink);
internal sealed record SentConfirmationEmail(string Email, string FullName, string ConfirmationLink);
internal sealed record SentInvitationEmail(string Email, string FullName, string ConfirmationLink);
internal sealed record SentPasswordSetupEmail(string Email, string FullName, string ResetLink);
