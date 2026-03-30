using System.Net;
using System.Reflection;
using System.Security.Claims;
using System.Text;
using AccountingSystem.API.Controllers;
using AccountingSystem.API.Data;
using AccountingSystem.API.Middleware;
using AccountingSystem.API.Models;
using AccountingSystem.API.Services;
using AccountingSystem.API.Services.Interfaces;
using AccountingSystem.Shared.DTOs;
using AccountingSystem.Shared.Enums;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace AccountingSystem.API.Tests;

public class ApplicationAuthorizationScopeEvaluatorTests
{
    [Fact]
    public void ScopePolicies_ShouldRequireExpectedRoleAndTenantContext()
    {
        var superAdmin = AuthorizationTestHelpers.CreatePrincipal(ApplicationRoles.SuperAdmin);
        var tenantOwner = AuthorizationTestHelpers.CreatePrincipal(ApplicationRoles.TenantOwner, companyId: 10);
        var accounting = AuthorizationTestHelpers.CreatePrincipal(ApplicationRoles.Accounting, companyId: 10);
        var management = AuthorizationTestHelpers.CreatePrincipal(ApplicationRoles.Management, companyId: 10);
        var tenantWithoutCompany = AuthorizationTestHelpers.CreatePrincipal(ApplicationRoles.TenantOwner);
        var anonymous = new ClaimsPrincipal(new ClaimsIdentity());

        ApplicationAuthorizationScopeEvaluator.IsSuperAdmin(superAdmin).Should().BeTrue();
        ApplicationAuthorizationScopeEvaluator.IsSuperAdmin(tenantOwner).Should().BeFalse();

        ApplicationAuthorizationScopeEvaluator.IsTenantOwner(tenantOwner).Should().BeTrue();
        ApplicationAuthorizationScopeEvaluator.IsTenantOwner(accounting).Should().BeFalse();

        ApplicationAuthorizationScopeEvaluator.HasTenantAccess(tenantOwner).Should().BeTrue();
        ApplicationAuthorizationScopeEvaluator.HasTenantAccess(accounting).Should().BeTrue();
        ApplicationAuthorizationScopeEvaluator.HasTenantAccess(management).Should().BeTrue();
        ApplicationAuthorizationScopeEvaluator.HasTenantAccess(superAdmin).Should().BeFalse();
        ApplicationAuthorizationScopeEvaluator.HasTenantAccess(tenantWithoutCompany).Should().BeFalse();
        ApplicationAuthorizationScopeEvaluator.HasTenantAccess(anonymous).Should().BeFalse();

        ApplicationAuthorizationScopeEvaluator.HasTenantAccountingAccess(tenantOwner).Should().BeTrue();
        ApplicationAuthorizationScopeEvaluator.HasTenantAccountingAccess(accounting).Should().BeTrue();
        ApplicationAuthorizationScopeEvaluator.HasTenantAccountingAccess(management).Should().BeFalse();

        ApplicationAuthorizationScopeEvaluator.HasTenantOperationalAccess(tenantOwner).Should().BeTrue();
        ApplicationAuthorizationScopeEvaluator.HasTenantOperationalAccess(accounting).Should().BeTrue();
        ApplicationAuthorizationScopeEvaluator.HasTenantOperationalAccess(management).Should().BeTrue();
        ApplicationAuthorizationScopeEvaluator.HasTenantOperationalAccess(superAdmin).Should().BeFalse();

        ApplicationAuthorizationScopeEvaluator.TryGetCompanyId(tenantOwner, out var companyId).Should().BeTrue();
        companyId.Should().Be(10);
        ApplicationAuthorizationScopeEvaluator.TryGetCompanyId(tenantWithoutCompany, out _).Should().BeFalse();
    }
}

public class ApplicationRoleAssignmentRulesTests
{
    [Fact]
    public void GetAssignableRoles_ShouldReturnExpectedRolesPerActor()
    {
        ApplicationRoleAssignmentRules.GetAssignableRoles(ApplicationRoles.SuperAdmin)
            .Should()
            .BeEquivalentTo(new[]
            {
                ApplicationRoles.SuperAdmin,
                ApplicationRoles.TenantOwner,
                ApplicationRoles.Accounting,
                ApplicationRoles.Management
            });

        ApplicationRoleAssignmentRules.GetAssignableRoles(ApplicationRoles.TenantOwner)
            .Should()
            .BeEquivalentTo(new[]
            {
                ApplicationRoles.Accounting,
                ApplicationRoles.Management
            });

        ApplicationRoleAssignmentRules.GetAssignableRoles(ApplicationRoles.Accounting).Should().BeEmpty();
        ApplicationRoleAssignmentRules.GetAssignableRoles(ApplicationRoles.Management).Should().BeEmpty();
    }

    [Theory]
    [InlineData(ApplicationRoles.SuperAdmin, ApplicationRoles.SuperAdmin, true)]
    [InlineData(ApplicationRoles.SuperAdmin, ApplicationRoles.TenantOwner, true)]
    [InlineData(ApplicationRoles.TenantOwner, ApplicationRoles.Accounting, true)]
    [InlineData(ApplicationRoles.TenantOwner, ApplicationRoles.Management, true)]
    [InlineData(ApplicationRoles.TenantOwner, ApplicationRoles.TenantOwner, false)]
    [InlineData(ApplicationRoles.TenantOwner, ApplicationRoles.SuperAdmin, false)]
    [InlineData(ApplicationRoles.Accounting, ApplicationRoles.Management, false)]
    [InlineData(ApplicationRoles.Management, ApplicationRoles.Accounting, false)]
    public void CanAssignRole_ShouldEnforceExpectedRoleMatrix(string actorRole, string targetRole, bool expected)
    {
        ApplicationRoleAssignmentRules.CanAssignRole(actorRole, targetRole).Should().Be(expected);
    }
}

public class TenantAccessMiddlewareTests
{
    [Fact]
    public async Task Invoke_WhenUserIsSuperAdmin_ShouldBypassTenantChecks()
    {
        using var context = TestHelpers.CreateContext();
        var nextCalled = false;
        var middleware = new TenantAccessMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        var httpContext = AuthorizationTestHelpers.CreateHttpContext(
            AuthorizationTestHelpers.CreatePrincipal(ApplicationRoles.SuperAdmin));

        await middleware.Invoke(httpContext, context);

        nextCalled.Should().BeTrue();
        httpContext.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
    }

    [Fact]
    public async Task Invoke_WhenTenantScopedUserLacksCompanyContext_ShouldReturnForbidden()
    {
        using var context = TestHelpers.CreateContext();
        var nextCalled = false;
        var middleware = new TenantAccessMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        var httpContext = AuthorizationTestHelpers.CreateHttpContext(
            AuthorizationTestHelpers.CreatePrincipal(ApplicationRoles.TenantOwner, userId: 41));

        await middleware.Invoke(httpContext, context);

        nextCalled.Should().BeFalse();
        httpContext.Response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        var response = await AuthorizationTestHelpers.ReadResponseAsync(httpContext.Response);
        response.Should().Contain("valid company context");
    }

    [Fact]
    public async Task Invoke_WhenUserIsBlocked_ShouldReturnForbidden()
    {
        using var context = TestHelpers.CreateContext(tenantId: 10);
        var role = new Role { Id = 1, Name = ApplicationRoles.TenantOwner };
        var company = new Company { Id = 10, Name = "Contoso", IsActive = true, Status = "Active" };
        var user = TestHelpers.CreateUser(role, company.Id, "owner@contoso.com", "LongPassword123!", status: "Blocked");

        context.Roles.Add(role);
        context.Companies.Add(company);
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var middleware = new TenantAccessMiddleware(_ => Task.CompletedTask);
        var httpContext = AuthorizationTestHelpers.CreateHttpContext(
            AuthorizationTestHelpers.CreatePrincipal(ApplicationRoles.TenantOwner, userId: user.Id, companyId: company.Id));

        await middleware.Invoke(httpContext, context);

        httpContext.Response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        var response = await AuthorizationTestHelpers.ReadResponseAsync(httpContext.Response);
        response.Should().Contain("blocked");
    }

    [Theory]
    [InlineData("Blocked", true, "permanently blocked")]
    [InlineData("Suspended", true, "suspended")]
    [InlineData("Active", false, "suspended")]
    public async Task Invoke_WhenCompanyIsNotUsable_ShouldReturnForbidden(string companyStatus, bool isActive, string expectedMessage)
    {
        using var context = TestHelpers.CreateContext(tenantId: 12);
        var role = new Role { Id = 1, Name = ApplicationRoles.TenantOwner };
        var company = new Company { Id = 12, Name = "Phase Three Co", IsActive = isActive, Status = companyStatus };
        var user = TestHelpers.CreateUser(role, company.Id, "owner@phase3.com", "LongPassword123!");

        context.Roles.Add(role);
        context.Companies.Add(company);
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var middleware = new TenantAccessMiddleware(_ => Task.CompletedTask);
        var httpContext = AuthorizationTestHelpers.CreateHttpContext(
            AuthorizationTestHelpers.CreatePrincipal(ApplicationRoles.TenantOwner, userId: user.Id, companyId: company.Id));

        await middleware.Invoke(httpContext, context);

        httpContext.Response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        var response = await AuthorizationTestHelpers.ReadResponseAsync(httpContext.Response);
        response.Should().Contain(expectedMessage);
    }
}

public class TenantBoundaryRegressionTests
{
    [Fact]
    public async Task UsersController_GetAllUsers_WhenIncludingArchived_ShouldRemainWithinCurrentTenant()
    {
        using var context = TestHelpers.CreateContext(tenantId: 10);
        var ownerRole = new Role { Id = 1, Name = ApplicationRoles.TenantOwner };
        var accountingRole = new Role { Id = 2, Name = ApplicationRoles.Accounting };

        var tenantUser = TestHelpers.CreateUser(accountingRole, 10, "tenant.user@test.com", "LongPassword123!");
        tenantUser.IsDeleted = true;
        tenantUser.IsActive = false;

        var foreignUser = TestHelpers.CreateUser(accountingRole, 20, "foreign.user@test.com", "LongPassword123!");
        foreignUser.IsDeleted = true;
        foreignUser.IsActive = false;

        context.Roles.AddRange(ownerRole, accountingRole);
        context.Users.AddRange(tenantUser, foreignUser);
        await context.SaveChangesAsync();

        var controller = new UsersController(
            context,
            Mock.Of<IAuthService>(),
            Mock.Of<ILegacyIdentityBridgeService>())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = AuthorizationTestHelpers.CreateHttpContext(
                    AuthorizationTestHelpers.CreatePrincipal(ApplicationRoles.TenantOwner, userId: 999, companyId: 10))
            }
        };

        var result = await controller.GetAllUsers(includeArchived: true);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var users = ok.Value.Should().BeAssignableTo<IEnumerable<UserDTO>>().Subject.ToList();
        users.Should().ContainSingle();
        users.Single().Email.Should().Be("tenant.user@test.com");
    }

    [Fact]
    public async Task UsersController_RestoreUser_WhenUserIsInvited_ShouldKeepTheAccountInactive()
    {
        using var context = TestHelpers.CreateContext(tenantId: 10);
        var ownerRole = new Role { Id = 1, Name = ApplicationRoles.TenantOwner };
        var accountingRole = new Role { Id = 2, Name = ApplicationRoles.Accounting };
        var invitedUser = new User
        {
            CompanyId = 10,
            Email = "invited.restore@test.com",
            FullName = "Invited Restore",
            RoleId = accountingRole.Id,
            Role = accountingRole,
            PasswordHash = string.Empty,
            PasswordSalt = null,
            IsDeleted = true,
            IsActive = false,
            Status = ApplicationUserStatuses.Invited
        };

        context.Roles.AddRange(ownerRole, accountingRole);
        context.Users.Add(invitedUser);
        await context.SaveChangesAsync();

        var controller = new UsersController(
            context,
            Mock.Of<IAuthService>(),
            Mock.Of<ILegacyIdentityBridgeService>())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = AuthorizationTestHelpers.CreateHttpContext(
                    AuthorizationTestHelpers.CreatePrincipal(ApplicationRoles.TenantOwner, userId: 999, companyId: 10))
            }
        };

        var result = await controller.RestoreUser(invitedUser.Id);

        result.Should().BeOfType<OkObjectResult>();
        var reloaded = await context.Users.IgnoreQueryFilters().SingleAsync(u => u.Id == invitedUser.Id);
        reloaded.IsDeleted.Should().BeFalse();
        reloaded.IsActive.Should().BeFalse();
        reloaded.Status.Should().Be(ApplicationUserStatuses.Invited);
    }

    [Fact]
    public async Task UsersController_DeleteUser_WhenTargetBelongsToAnotherTenant_ShouldReturnNotFound()
    {
        using var context = TestHelpers.CreateContext(tenantId: 10);
        var ownerRole = new Role { Id = 1, Name = ApplicationRoles.TenantOwner };
        var accountingRole = new Role { Id = 2, Name = ApplicationRoles.Accounting };
        var foreignUser = TestHelpers.CreateUser(accountingRole, 20, "foreign.user@test.com", "LongPassword123!");

        context.Roles.AddRange(ownerRole, accountingRole);
        context.Users.Add(foreignUser);
        await context.SaveChangesAsync();

        var controller = new UsersController(
            context,
            Mock.Of<IAuthService>(),
            Mock.Of<ILegacyIdentityBridgeService>())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = AuthorizationTestHelpers.CreateHttpContext(
                    AuthorizationTestHelpers.CreatePrincipal(ApplicationRoles.TenantOwner, userId: 999, companyId: 10))
            }
        };

        var result = await controller.DeleteUser(foreignUser.Id);

        result.Should().BeOfType<NotFoundObjectResult>();
        var reloaded = await context.Users.IgnoreQueryFilters().SingleAsync(u => u.Id == foreignUser.Id);
        reloaded.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public async Task UsersController_RestoreUser_WhenTargetIsTenantOwner_ShouldRejectTheRequest()
    {
        using var context = TestHelpers.CreateContext(tenantId: 10);
        var ownerRole = new Role { Id = 1, Name = ApplicationRoles.TenantOwner };
        var peerTenantOwner = new User
        {
            CompanyId = 10,
            Email = "peer.owner@test.com",
            FullName = "Peer Owner",
            RoleId = ownerRole.Id,
            Role = ownerRole,
            PasswordHash = string.Empty,
            PasswordSalt = null,
            IsDeleted = true,
            IsActive = false,
            Status = ApplicationUserStatuses.Invited
        };

        context.Roles.Add(ownerRole);
        context.Users.Add(peerTenantOwner);
        await context.SaveChangesAsync();

        var controller = new UsersController(
            context,
            Mock.Of<IAuthService>(),
            Mock.Of<ILegacyIdentityBridgeService>())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = AuthorizationTestHelpers.CreateHttpContext(
                    AuthorizationTestHelpers.CreatePrincipal(ApplicationRoles.TenantOwner, userId: 999, companyId: 10))
            }
        };

        var result = await controller.RestoreUser(peerTenantOwner.Id);

        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequest.Value.Should().NotBeNull();
        badRequest.Value!.ToString().Should().Contain("Tenant owner accounts can only be managed by Super Admin.");
    }

    [Fact]
    public async Task PayableService_RestoreVendorAsync_ShouldNotRestoreAnotherTenantVendor()
    {
        using var context = TestHelpers.CreateContext(tenantId: 10);
        var tenantService = new Mock<ITenantService>();
        tenantService.Setup(x => x.GetCurrentTenant()).Returns(10);

        var foreignVendor = new Vendor
        {
            Id = 5,
            CompanyId = 20,
            Name = "Foreign Vendor",
            IsDeleted = true,
            IsActive = false
        };

        context.Vendors.Add(foreignVendor);
        await context.SaveChangesAsync();

        var service = new PayableService(
            context,
            Mock.Of<ILedgerService>(),
            Mock.Of<IYearEndCloseService>(),
            Mock.Of<IDocumentSequenceService>(),
            tenantService.Object);

        var act = async () => await service.RestoreVendorAsync(foreignVendor.Id);

        await act.Should().ThrowAsync<Exception>().WithMessage("Vendor not found");

        var reloaded = await context.Vendors.IgnoreQueryFilters().SingleAsync(v => v.Id == foreignVendor.Id);
        reloaded.IsDeleted.Should().BeTrue();
        reloaded.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task ReceivableService_RestoreCustomerAsync_ShouldNotRestoreAnotherTenantCustomer()
    {
        using var context = TestHelpers.CreateContext(tenantId: 10);
        var tenantService = new Mock<ITenantService>();
        tenantService.Setup(x => x.GetCurrentTenant()).Returns(10);

        var foreignCustomer = new Customer
        {
            Id = 7,
            CompanyId = 21,
            Name = "Foreign Customer",
            IsDeleted = true,
            IsActive = false
        };

        context.Customers.Add(foreignCustomer);
        await context.SaveChangesAsync();

        var service = new ReceivableService(
            context,
            Mock.Of<ILedgerService>(),
            Mock.Of<IPaymentService>(),
            Mock.Of<IYearEndCloseService>(),
            Mock.Of<IDocumentSequenceService>(),
            tenantService.Object);

        var act = async () => await service.RestoreCustomerAsync(foreignCustomer.Id);

        await act.Should().ThrowAsync<Exception>().WithMessage("Customer not found");

        var reloaded = await context.Customers.IgnoreQueryFilters().SingleAsync(c => c.Id == foreignCustomer.Id);
        reloaded.IsDeleted.Should().BeTrue();
        reloaded.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task LedgerService_RestoreAccountAsync_ShouldNotRestoreAnotherTenantAccount()
    {
        using var context = TestHelpers.CreateContext(tenantId: 10);
        var tenantService = new Mock<ITenantService>();
        tenantService.Setup(x => x.GetCurrentTenant()).Returns(10);

        var foreignAccount = new Account
        {
            Id = 9,
            CompanyId = 22,
            Code = "1999",
            Name = "Foreign Account",
            Type = "Asset",
            IsDeleted = true,
            IsActive = false
        };

        context.Accounts.Add(foreignAccount);
        await context.SaveChangesAsync();

        var service = new LedgerService(
            context,
            Mock.Of<IYearEndCloseService>(),
            tenantService.Object,
            Mock.Of<IDocumentSequenceService>());

        var act = async () => await service.RestoreAccountAsync(foreignAccount.Id);

        await act.Should().ThrowAsync<Exception>().WithMessage("Account not found");

        var reloaded = await context.Accounts.IgnoreQueryFilters().SingleAsync(a => a.Id == foreignAccount.Id);
        reloaded.IsDeleted.Should().BeTrue();
        reloaded.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task DocumentSequenceService_WhenCompanyIdIsInvalid_ShouldRejectTheRequest()
    {
        using var context = TestHelpers.CreateContext();
        var service = new DocumentSequenceService(context);

        var act = async () => await service.GetSequencesAsync(0);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Document numbering requires a valid tenant company context.");
    }
}

public class LogScopeAlignmentTests
{
    [Fact]
    public void LogControllers_ShouldKeepExplicitScopePolicies()
    {
        var superAdminPolicy = typeof(SuperAdminController)
            .GetCustomAttribute<AuthorizeAttribute>()?
            .Policy;
        var tenantAuditPolicy = typeof(AuditLogsController)
            .GetCustomAttribute<AuthorizeAttribute>()?
            .Policy;

        superAdminPolicy.Should().Be(ApplicationAuthorizationPolicies.RequireSuperAdmin);
        tenantAuditPolicy.Should().Be(ApplicationAuthorizationPolicies.RequireTenantOwner);
    }

    [Fact]
    public async Task GetPlatformSecurityEvents_ShouldReturnOnlyAuthEventsWithTenantAndIpContext()
    {
        using var context = TestHelpers.CreateContext();

        var tenantOwnerRole = new Role { Id = 1, Name = ApplicationRoles.TenantOwner };
        var company = new Company { Id = 10, Name = "Contoso Books", IsActive = true, Status = "Active" };
        var user = TestHelpers.CreateUser(tenantOwnerRole, company.Id, "owner@contoso.test", "LongPassword123!");

        context.Roles.Add(tenantOwnerRole);
        context.Companies.Add(company);
        context.Users.Add(user);
        await context.SaveChangesAsync();

        context.AuditLogs.AddRange(
            new AuditLog
            {
                CompanyId = company.Id,
                UserId = user.Id,
                Action = "AUTH-LOGIN-FAILURE",
                EntityName = "/api/auth/login",
                EntityId = "N/A",
                IpAddress = "203.0.113.10",
                Timestamp = DateTime.UtcNow,
                Changes = "{\"reason\":\"InvalidPassword\"}"
            },
            new AuditLog
            {
                CompanyId = 0,
                UserId = null,
                Action = "AUTH-RATE-LIMIT",
                EntityName = "/api/auth/login",
                EntityId = "N/A",
                IpAddress = null,
                Timestamp = DateTime.UtcNow.AddMinutes(-1),
                Changes = "{\"email\":\"anonymous@test.com\",\"remoteIpAddress\":\"198.51.100.20\"}"
            },
            new AuditLog
            {
                CompanyId = company.Id,
                UserId = user.Id,
                Action = "INVOICE-CREATE",
                EntityName = "/api/invoices",
                EntityId = "N/A",
                IpAddress = "198.51.100.25",
                Timestamp = DateTime.UtcNow.AddMinutes(-2),
                Changes = "{}"
            });
        await context.SaveChangesAsync();

        var controller = new SuperAdminController(
            context,
            Mock.Of<ILogger<SuperAdminController>>(),
            Mock.Of<ILegacyIdentityBridgeService>())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = AuthorizationTestHelpers.CreateHttpContext(
                    AuthorizationTestHelpers.CreatePrincipal(ApplicationRoles.SuperAdmin, userId: 9001))
            }
        };

        var result = await controller.GetPlatformSecurityEvents();

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var events = ok.Value.Should().BeAssignableTo<IEnumerable<PlatformSecurityEventDTO>>().Subject.ToList();
        events.Should().HaveCount(2);
        events.Should().OnlyContain(log => log.Action.StartsWith("AUTH-", StringComparison.Ordinal));
        events.Should().Contain(log =>
            log.CompanyName == "Contoso Books" &&
            log.UserEmail == "owner@contoso.test" &&
            log.IpAddress == "203.0.113.10");
        events.Should().Contain(log =>
            log.CompanyName == "Platform / Unknown" &&
            log.UserEmail == "anonymous@test.com" &&
            log.IpAddress == "198.51.100.20");
    }

    [Fact]
    public async Task GetAuditLogs_ShouldReturnTenantScopedLogsWithIpAddresses()
    {
        using var context = TestHelpers.CreateContext(tenantId: 10);

        var tenantOwnerRole = new Role { Id = 1, Name = ApplicationRoles.TenantOwner };
        var accountingRole = new Role { Id = 2, Name = ApplicationRoles.Accounting };
        var activeUser = TestHelpers.CreateUser(accountingRole, 10, "accounting@tenant.test", "LongPassword123!");
        var archivedUser = TestHelpers.CreateUser(accountingRole, 10, "archived@tenant.test", "LongPassword123!");
        archivedUser.IsDeleted = true;

        context.Roles.AddRange(tenantOwnerRole, accountingRole);
        context.Users.AddRange(activeUser, archivedUser);
        await context.SaveChangesAsync();

        context.AuditLogs.AddRange(
            new AuditLog
            {
                CompanyId = 10,
                UserId = activeUser.Id,
                Action = "USER-ARCHIVE",
                EntityName = "/api/users/2",
                EntityId = "N/A",
                IpAddress = "192.0.2.10",
                Timestamp = DateTime.UtcNow,
                Changes = "{}"
            },
            new AuditLog
            {
                CompanyId = 10,
                UserId = null,
                Action = "AUTH-LOGIN-FAILURE",
                EntityName = "/api/auth/login",
                EntityId = "N/A",
                IpAddress = null,
                Timestamp = DateTime.UtcNow.AddMinutes(-1),
                Changes = "{\"email\":\"failed-login@tenant.test\",\"remoteIpAddress\":\"192.0.2.11\"}"
            });
        await context.SaveChangesAsync();

        var controller = new AuditLogsController(context);
        var result = await controller.GetAuditLogs();

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var logs = ok.Value.Should().BeAssignableTo<IEnumerable<AuditLogDTO>>().Subject.ToList();
        logs.Should().HaveCount(2);
        logs.Should().Contain(log =>
            log.UserEmail == "accounting@tenant.test" &&
            log.IpAddress == "192.0.2.10");
        logs.Should().Contain(log =>
            log.UserEmail == "failed-login@tenant.test" &&
            log.IpAddress == "192.0.2.11");
    }

    [Fact]
    public async Task AuthSecurityAuditService_ShouldPersistIpAddressOnNewSecurityEvents()
    {
        using var context = TestHelpers.CreateContext();
        var httpContext = new DefaultHttpContext();
        httpContext.Connection.RemoteIpAddress = IPAddress.Parse("198.51.100.77");
        httpContext.User = AuthorizationTestHelpers.CreatePrincipal(ApplicationRoles.TenantOwner, userId: 55, companyId: 12);

        var accessor = new HttpContextAccessor { HttpContext = httpContext };
        var service = new AuthSecurityAuditService(
            context,
            accessor,
            Mock.Of<ILogger<AuthSecurityAuditService>>());

        await service.WriteAsync("AUTH-LOGIN-SUCCESS");

        var log = await context.AuditLogs.IgnoreQueryFilters().SingleAsync();
        log.IpAddress.Should().Be("198.51.100.77");
    }

    [Fact]
    public async Task UpdateCompanyStatus_ShouldWriteIpAddressToSuperAdminAuditLog()
    {
        using var context = TestHelpers.CreateContext();
        var company = new Company { Id = 99, Name = "Northwind", IsActive = true, Status = "Active" };
        context.Companies.Add(company);
        await context.SaveChangesAsync();

        var controller = new SuperAdminController(
            context,
            Mock.Of<ILogger<SuperAdminController>>(),
            Mock.Of<ILegacyIdentityBridgeService>())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = AuthorizationTestHelpers.CreateHttpContext(
                    AuthorizationTestHelpers.CreatePrincipal(ApplicationRoles.SuperAdmin, userId: 7001))
            }
        };
        controller.HttpContext.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.55");

        var result = await controller.UpdateCompanyStatus(company.Id, new UpdateCompanyStatusDTO { Status = "Suspended" });

        result.Should().BeOfType<OkObjectResult>();
        var auditLog = await context.SuperAdminAuditLogs.SingleAsync();
        auditLog.IpAddress.Should().Be("203.0.113.55");
    }
}

internal static class AuthorizationTestHelpers
{
    internal static ClaimsPrincipal CreatePrincipal(string role, int? userId = null, int? companyId = null)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, "scope@test.com"),
            new(ClaimTypes.Role, role),
            new("role", role)
        };

        if (userId.HasValue)
        {
            claims.Add(new Claim("UserId", userId.Value.ToString()));
        }

        if (companyId.HasValue)
        {
            claims.Add(new Claim("CompanyId", companyId.Value.ToString()));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test", ClaimTypes.Name, ClaimTypes.Role));
    }

    internal static DefaultHttpContext CreateHttpContext(ClaimsPrincipal user)
    {
        var context = new DefaultHttpContext
        {
            User = user
        };
        context.Response.Body = new MemoryStream();
        return context;
    }

    internal static async Task<string> ReadResponseAsync(HttpResponse response)
    {
        response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(response.Body, Encoding.UTF8, leaveOpen: true);
        return await reader.ReadToEndAsync();
    }
}
