using AccountingSystem.API.Configuration;
using AccountingSystem.API.Controllers;
using AccountingSystem.API.Data;
using AccountingSystem.API.Models;
using AccountingSystem.API.Services;
using AccountingSystem.API.Services.Interfaces;
using AccountingSystem.Shared.DTOs;
using AccountingSystem.Shared.Security;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace AccountingSystem.API.Tests;

public class LoginAttemptPolicyTests
{
    [Fact]
    public async Task LoginPolicy_ShouldApplyFirstLockoutOnFifthFailureWithinAttemptWindow()
    {
        var timeProvider = new MutableTimeProvider(DateTimeOffset.Parse("2026-04-02T00:00:00Z"));
        using var context = TestHelpers.CreateContext(tenantId: 50);
        using var harness = TestHelpers.CreateIdentityHarness();
        var service = TestHelpers.CreateAuthService(context, harness, timeProvider: timeProvider);

        var controller = new AuthController(service);
        var user = await CreateConfirmedUserAsync(context, harness, 50, "lockout1@test.com");

        for (var attempt = 1; attempt <= 4; attempt++)
        {
            var unauthorized = await controller.Login(new LoginDTO
            {
                Email = user.Email,
                Password = "WrongPassword123!"
            });

            var payload = unauthorized.Should().BeOfType<UnauthorizedObjectResult>().Subject.Value
                .Should().BeAssignableTo<AuthFailureResponseDTO>().Subject;
            payload.ErrorCode.Should().Be(AuthFailureErrorCodes.InvalidCredentials);

            var refreshedUser = await context.Users.IgnoreQueryFilters().SingleAsync(u => u.Id == user.Id);
            refreshedUser.AccessFailedCount.Should().Be(attempt);
            refreshedUser.LockoutEndUtc.Should().BeNull();
        }

        var fifthAttempt = await controller.Login(new LoginDTO
        {
            Email = user.Email,
            Password = "WrongPassword123!"
        });

        var locked = fifthAttempt.Should().BeOfType<ObjectResult>().Subject;
        locked.StatusCode.Should().Be(StatusCodes.Status423Locked);

        var lockoutPayload = locked.Value.Should().BeAssignableTo<AuthFailureResponseDTO>().Subject;
        lockoutPayload.ErrorCode.Should().Be(AuthFailureErrorCodes.TemporaryLockout);
        lockoutPayload.LockoutEndUtc.Should().Be(timeProvider.GetUtcNow().UtcDateTime.AddMinutes(5));
        lockoutPayload.RemainingSeconds.Should().BeInRange(299, 300);

        var lockedUser = await context.Users.IgnoreQueryFilters().SingleAsync(u => u.Id == user.Id);
        lockedUser.AccessFailedCount.Should().Be(5);
        lockedUser.LockoutEndUtc.Should().Be(timeProvider.GetUtcNow().UtcDateTime.AddMinutes(5));
    }

    [Fact]
    public async Task LoginPolicy_ShouldEscalateConsecutiveLockouts_AndResetAfterFullSuccess()
    {
        var timeProvider = new MutableTimeProvider(DateTimeOffset.Parse("2026-04-02T00:00:00Z"));
        using var context = TestHelpers.CreateContext(tenantId: 51);
        using var harness = TestHelpers.CreateIdentityHarness();
        var service = TestHelpers.CreateAuthService(context, harness, timeProvider: timeProvider);
        var controller = new AuthController(service);
        var user = await CreateConfirmedUserAsync(context, harness, 51, "lockout2@test.com");

        var firstLockout = await TriggerLockoutAsync(controller, user.Email, "WrongPassword123!", attemptsUntilThreshold: 5);
        firstLockout.LockoutEndUtc.Should().Be(timeProvider.GetUtcNow().UtcDateTime.AddMinutes(5));

        timeProvider.Advance(TimeSpan.FromMinutes(5).Add(TimeSpan.FromSeconds(1)));

        var secondLockoutResult = await controller.Login(new LoginDTO
        {
            Email = user.Email,
            Password = "WrongPassword123!"
        });

        var secondLockout = secondLockoutResult.Should().BeOfType<ObjectResult>().Subject.Value
            .Should().BeAssignableTo<AuthFailureResponseDTO>().Subject;
        secondLockout.ErrorCode.Should().Be(AuthFailureErrorCodes.TemporaryLockout);
        secondLockout.LockoutEndUtc.Should().Be(timeProvider.GetUtcNow().UtcDateTime.AddMinutes(15));
        secondLockout.RemainingSeconds.Should().BeInRange(899, 900);

        timeProvider.Advance(TimeSpan.FromMinutes(15).Add(TimeSpan.FromSeconds(1)));

        var thirdLockout = await TriggerLockoutAsync(controller, user.Email, "WrongPassword123!", attemptsUntilThreshold: 5);
        thirdLockout.LockoutEndUtc.Should().Be(timeProvider.GetUtcNow().UtcDateTime.AddMinutes(30));
        thirdLockout.RemainingSeconds.Should().BeInRange(1799, 1800);

        var stateAfterThirdLockout = await context.UserLoginSecurityStates.SingleAsync(s => s.UserId == user.Id);
        stateAfterThirdLockout.ConsecutiveLockoutCount.Should().Be(3);

        timeProvider.Advance(TimeSpan.FromMinutes(30).Add(TimeSpan.FromSeconds(1)));

        var success = await service.LoginAsync(new LoginDTO
        {
            Email = user.Email,
            Password = "LongPassword123!"
        });
        success.Token.Should().NotBeNullOrWhiteSpace();

        var stateAfterSuccess = await context.UserLoginSecurityStates.SingleAsync(s => s.UserId == user.Id);
        stateAfterSuccess.ConsecutiveLockoutCount.Should().Be(0);

        for (var attempt = 1; attempt <= 4; attempt++)
        {
            var response = await controller.Login(new LoginDTO
            {
                Email = user.Email,
                Password = "WrongPassword123!"
            });

            response.Should().BeOfType<UnauthorizedObjectResult>();
        }

        var resetLockoutResult = await controller.Login(new LoginDTO
        {
            Email = user.Email,
            Password = "WrongPassword123!"
        });

        var resetLockout = resetLockoutResult.Should().BeOfType<ObjectResult>().Subject.Value
            .Should().BeAssignableTo<AuthFailureResponseDTO>().Subject;
        resetLockout.ErrorCode.Should().Be(AuthFailureErrorCodes.TemporaryLockout);
        resetLockout.LockoutEndUtc.Should().Be(timeProvider.GetUtcNow().UtcDateTime.AddMinutes(5));
    }

    [Fact]
    public async Task LoginPolicy_ShouldAutoDisableAfterFiveLockoutEvents_AndAllowAdminEnable()
    {
        var timeProvider = new MutableTimeProvider(DateTimeOffset.Parse("2026-04-02T00:00:00Z"));
        using var context = TestHelpers.CreateContext(tenantId: 52);
        using var harness = TestHelpers.CreateIdentityHarness();
        var auditService = new Mock<IAuthSecurityAuditService>();
        auditService.Setup(x => x.WriteAsync(
                It.IsAny<string>(),
                It.IsAny<int?>(),
                It.IsAny<int?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<int?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<string?>(),
                It.IsAny<int?>(),
                It.IsAny<int?>(),
                It.IsAny<string?>()))
            .Returns(Task.CompletedTask);

        var service = TestHelpers.CreateAuthService(context, harness, auditService: auditService, timeProvider: timeProvider);
        var controller = new AuthController(service);
        var user = await CreateConfirmedUserAsync(context, harness, 52, "autodisable@test.com");

        await TriggerLockoutAsync(controller, user.Email, "WrongPassword123!", attemptsUntilThreshold: 5);
        timeProvider.Advance(TimeSpan.FromMinutes(5).Add(TimeSpan.FromSeconds(1)));

        await controller.Login(new LoginDTO { Email = user.Email, Password = "WrongPassword123!" });
        timeProvider.Advance(TimeSpan.FromMinutes(15).Add(TimeSpan.FromSeconds(1)));

        await TriggerLockoutAsync(controller, user.Email, "WrongPassword123!", attemptsUntilThreshold: 5);
        timeProvider.Advance(TimeSpan.FromMinutes(30).Add(TimeSpan.FromSeconds(1)));

        await TriggerLockoutAsync(controller, user.Email, "WrongPassword123!", attemptsUntilThreshold: 5);
        timeProvider.Advance(TimeSpan.FromMinutes(30).Add(TimeSpan.FromSeconds(1)));

        var autoDisabledResult = await TriggerFinalDisableAsync(controller, user.Email, "WrongPassword123!");
        autoDisabledResult.ErrorCode.Should().Be(AuthFailureErrorCodes.AccountDisabled);
        autoDisabledResult.Disabled.Should().BeTrue();

        var blockedUser = await context.Users.IgnoreQueryFilters().SingleAsync(u => u.Id == user.Id);
        blockedUser.Status.Should().Be(ApplicationUserStatuses.Blocked);
        blockedUser.IsActive.Should().BeFalse();

        var securityState = await context.UserLoginSecurityStates.SingleAsync(s => s.UserId == user.Id);
        securityState.DisabledReason.Should().Be(ApplicationUserDisableReasons.RepeatedLockouts);
        securityState.DisabledAtUtc.Should().NotBeNull();

        var blockedAttempt = await controller.Login(new LoginDTO
        {
            Email = user.Email,
            Password = "LongPassword123!"
        });

        var blockedPayload = blockedAttempt.Should().BeOfType<ObjectResult>().Subject.Value
            .Should().BeAssignableTo<AuthFailureResponseDTO>().Subject;
        blockedPayload.ErrorCode.Should().Be(AuthFailureErrorCodes.AccountDisabled);

        var loginSecurityService = new LoginSecurityService(
            context,
            harness.IdentityContext,
            auditService.Object,
            Options.Create(new LoginAttemptPolicyOptions
            {
                AttemptWindowMinutes = 15,
                MaxFailedAccessAttempts = 5,
                FirstLockoutMinutes = 5,
                SecondLockoutMinutes = 15,
                SubsequentLockoutMinutes = 30,
                DisableAfterLockoutEvents = 5,
                DisableWindowHours = 24
            }),
            timeProvider,
            Mock.Of<ILogger<LoginSecurityService>>());

        var superAdminController = new SuperAdminController(
            context,
            Mock.Of<ILogger<SuperAdminController>>(),
            Mock.Of<ILegacyIdentityBridgeService>(),
            loginSecurityService)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = AuthorizationTestHelpers.CreateHttpContext(
                    AuthorizationTestHelpers.CreatePrincipal(ApplicationRoles.SuperAdmin, userId: 9001))
            }
        };

        var enableResult = await superAdminController.UpdateUserStatus(user.Id, new UpdateUserStatusDTO
        {
            Status = ApplicationUserStatuses.Active
        });

        enableResult.Should().BeOfType<OkObjectResult>();

        var reenabledUser = await context.Users.IgnoreQueryFilters().SingleAsync(u => u.Id == user.Id);
        reenabledUser.Status.Should().Be(ApplicationUserStatuses.Active);
        reenabledUser.IsActive.Should().BeTrue();
        reenabledUser.AccessFailedCount.Should().Be(0);
        reenabledUser.LockoutEndUtc.Should().BeNull();

        var reenabledState = await context.UserLoginSecurityStates.SingleAsync(s => s.UserId == user.Id);
        reenabledState.DisabledReason.Should().BeNull();
        reenabledState.ConsecutiveLockoutCount.Should().Be(0);

        var success = await service.LoginAsync(new LoginDTO
        {
            Email = user.Email,
            Password = "LongPassword123!"
        });

        success.Token.Should().NotBeNullOrWhiteSpace();

        auditService.Verify(x => x.WriteAsync(
                "AUTH-ACCOUNT-AUTO-DISABLED",
                It.IsAny<int?>(),
                It.IsAny<int?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<int?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<string?>(),
                It.IsAny<int?>(),
                It.IsAny<int?>(),
                It.IsAny<string?>()),
            Times.Once);

        auditService.Verify(x => x.WriteAsync(
                "AUTH-ACCOUNT-ENABLED-ADMIN",
                It.IsAny<int?>(),
                It.IsAny<int?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<int?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<string?>(),
                It.IsAny<int?>(),
                It.IsAny<int?>(),
                It.IsAny<string?>()),
            Times.Once);
    }

    [Fact]
    public async Task LoginPolicy_ShouldResetOnlyAfterFinalMfaSuccess()
    {
        var timeProvider = new MutableTimeProvider(DateTimeOffset.Parse("2026-04-02T00:00:00Z"));
        using var context = TestHelpers.CreateContext(tenantId: 53);
        using var harness = TestHelpers.CreateIdentityHarness();
        var service = TestHelpers.CreateAuthService(context, harness, timeProvider: timeProvider);
        var controller = new AuthController(service);
        var user = await CreateConfirmedUserAsync(context, harness, 53, "mfa-reset@test.com");

        var setup = await service.BeginAuthenticatorSetupAsync(user.Id);
        await service.VerifyAuthenticatorSetupAsync(user.Id, new VerifyAuthenticatorSetupDTO
        {
            Code = TestHelpers.GenerateAuthenticatorCode(setup.SharedKey)
        });

        await TriggerLockoutAsync(controller, user.Email, "WrongPassword123!", attemptsUntilThreshold: 5);
        timeProvider.Advance(TimeSpan.FromMinutes(5).Add(TimeSpan.FromSeconds(1)));

        await controller.Login(new LoginDTO
        {
            Email = user.Email,
            Password = "WrongPassword123!"
        });

        timeProvider.Advance(TimeSpan.FromMinutes(15).Add(TimeSpan.FromSeconds(1)));

        var loginChallenge = await service.LoginAsync(new LoginDTO
        {
            Email = user.Email,
            Password = "LongPassword123!"
        });

        loginChallenge.RequiresTwoFactor.Should().BeTrue();

        var stateBeforeMfa = await context.UserLoginSecurityStates.SingleAsync(s => s.UserId == user.Id);
        stateBeforeMfa.ConsecutiveLockoutCount.Should().Be(2);

        var mfaResponse = await service.CompleteMfaLoginAsync(new LoginMfaDTO
        {
            ChallengeToken = loginChallenge.TwoFactorChallengeToken,
            TwoFactorCode = TestHelpers.GenerateAuthenticatorCode(setup.SharedKey)
        });

        mfaResponse.Token.Should().NotBeNullOrWhiteSpace();

        var stateAfterMfa = await context.UserLoginSecurityStates.SingleAsync(s => s.UserId == user.Id);
        stateAfterMfa.ConsecutiveLockoutCount.Should().Be(0);

        var refreshedUser = await context.Users.IgnoreQueryFilters().SingleAsync(u => u.Id == user.Id);
        refreshedUser.AccessFailedCount.Should().Be(0);
        refreshedUser.LockoutEndUtc.Should().BeNull();
    }

    private static async Task<User> CreateConfirmedUserAsync(
        AccountingDbContext context,
        IdentityTestHarness harness,
        int companyId,
        string email)
    {
        var role = new Role { Id = companyId, Name = ApplicationRoles.TenantOwner };
        var company = new Company { Id = companyId, Name = $"Company {companyId}", IsActive = true, Status = "Active" };
        var user = TestHelpers.CreateUser(role, company.Id, email, "LongPassword123!");

        context.Roles.Add(role);
        context.Companies.Add(company);
        context.Users.Add(user);
        await context.SaveChangesAsync();

        await harness.AccountService.EnsureProvisionedAsync(
            TestHelpers.CreateIdentitySnapshot(user, role.Name, requireEmailConfirmation: true, emailConfirmed: true),
            "LongPassword123!");

        return user;
    }

    private static async Task<AuthFailureResponseDTO> TriggerLockoutAsync(
        AuthController controller,
        string email,
        string password,
        int attemptsUntilThreshold)
    {
        for (var attempt = 1; attempt < attemptsUntilThreshold; attempt++)
        {
            var response = await controller.Login(new LoginDTO
            {
                Email = email,
                Password = password
            });

            response.Should().BeOfType<UnauthorizedObjectResult>();
        }

        var finalResponse = await controller.Login(new LoginDTO
        {
            Email = email,
            Password = password
        });

        var objectResult = finalResponse.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status423Locked);
        return objectResult.Value.Should().BeAssignableTo<AuthFailureResponseDTO>().Subject;
    }

    private static async Task<AuthFailureResponseDTO> TriggerFinalDisableAsync(
        AuthController controller,
        string email,
        string password)
    {
        for (var attempt = 1; attempt <= 4; attempt++)
        {
            var response = await controller.Login(new LoginDTO
            {
                Email = email,
                Password = password
            });

            response.Should().BeOfType<UnauthorizedObjectResult>();
        }

        var finalResponse = await controller.Login(new LoginDTO
        {
            Email = email,
            Password = password
        });

        var objectResult = finalResponse.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        return objectResult.Value.Should().BeAssignableTo<AuthFailureResponseDTO>().Subject;
    }
}
