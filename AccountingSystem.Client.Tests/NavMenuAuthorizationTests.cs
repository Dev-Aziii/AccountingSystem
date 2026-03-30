using System.Security.Claims;
using AccountingSystem.Client.Layout;
using AccountingSystem.Shared.Security;
using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MudBlazor.Services;

namespace AccountingSystem.Client.Tests;

public class NavMenuAuthorizationTests : BunitContext
{
    public NavMenuAuthorizationTests()
    {
        Services.AddMudServices();
    }

    [Fact]
    public void SuperAdmin_ShouldOnlySeePlatformNavigation()
    {
        ConfigureAuthorization(CreatePrincipal(ApplicationRoles.SuperAdmin));

        var cut = Render(builder =>
        {
            builder.OpenComponent<CascadingAuthenticationState>(0);
            builder.AddAttribute(1, "ChildContent", (RenderFragment)(childBuilder =>
            {
                childBuilder.OpenComponent<NavMenu>(0);
                childBuilder.CloseComponent();
            }));
            builder.CloseComponent();
        });

            cut.WaitForAssertion(() =>
        {
            cut.Markup.Should().Contain("System Overview");
            cut.Markup.Should().Contain("Tenant Manager");
            cut.Markup.Should().Contain("Global Users");
            cut.Markup.Should().Contain("Platform Logs");
            cut.Markup.Should().NotContain("Dashboard");
            cut.Markup.Should().NotContain("User Management");
            cut.Markup.Should().NotContain("Bills");
            cut.Markup.Should().NotContain("Financial Statements");
        });
    }

    [Fact]
    public void TenantOwner_ShouldSeeTenantAdministrationAndOperationalNavigation()
    {
        ConfigureAuthorization(CreatePrincipal(ApplicationRoles.TenantOwner, companyId: 42));

        var cut = Render(builder =>
        {
            builder.OpenComponent<CascadingAuthenticationState>(0);
            builder.AddAttribute(1, "ChildContent", (RenderFragment)(childBuilder =>
            {
                childBuilder.OpenComponent<NavMenu>(0);
                childBuilder.CloseComponent();
            }));
            builder.CloseComponent();
        });

            cut.WaitForAssertion(() =>
        {
            cut.Markup.Should().Contain("Dashboard");
            cut.Markup.Should().Contain("User Management");
            cut.Markup.Should().Contain("Tenant Audit Logs");
            cut.Markup.Should().Contain("Company Settings");
            cut.Markup.Should().Contain("Journal Entries");
            cut.Markup.Should().Contain("Chart of Accounts");
            cut.Markup.Should().Contain("Bills");
            cut.Markup.Should().Contain("Invoices");
            cut.Markup.Should().Contain("Financial Statements");
            cut.Markup.Should().NotContain("System Overview");
            cut.Markup.Should().NotContain("Platform Logs");
        });
    }

    [Fact]
    public void Accounting_ShouldSeeAccountingAndOperationalNavigationButNotTenantAdministration()
    {
        ConfigureAuthorization(CreatePrincipal(ApplicationRoles.Accounting, companyId: 42));

        var cut = Render(builder =>
        {
            builder.OpenComponent<CascadingAuthenticationState>(0);
            builder.AddAttribute(1, "ChildContent", (RenderFragment)(childBuilder =>
            {
                childBuilder.OpenComponent<NavMenu>(0);
                childBuilder.CloseComponent();
            }));
            builder.CloseComponent();
        });

        cut.WaitForAssertion(() =>
        {
            cut.Markup.Should().Contain("Dashboard");
            cut.Markup.Should().Contain("Journal Entries");
            cut.Markup.Should().Contain("Chart of Accounts");
            cut.Markup.Should().Contain("Bills");
            cut.Markup.Should().Contain("Invoices");
            cut.Markup.Should().Contain("Financial Statements");
            cut.Markup.Should().NotContain("User Management");
            cut.Markup.Should().NotContain("Tenant Audit Logs");
            cut.Markup.Should().NotContain("Platform Logs");
            cut.Markup.Should().NotContain("System Overview");
        });
    }

    [Fact]
    public void Management_ShouldOnlySeeOperationalNavigation()
    {
        ConfigureAuthorization(CreatePrincipal(ApplicationRoles.Management, companyId: 42));

        var cut = Render(builder =>
        {
            builder.OpenComponent<CascadingAuthenticationState>(0);
            builder.AddAttribute(1, "ChildContent", (RenderFragment)(childBuilder =>
            {
                childBuilder.OpenComponent<NavMenu>(0);
                childBuilder.CloseComponent();
            }));
            builder.CloseComponent();
        });

        cut.WaitForAssertion(() =>
        {
            cut.Markup.Should().Contain("Dashboard");
            cut.Markup.Should().Contain("Financial Statements");
            cut.Markup.Should().NotContain("User Management");
            cut.Markup.Should().NotContain("Tenant Audit Logs");
            cut.Markup.Should().NotContain("Platform Logs");
            cut.Markup.Should().NotContain("Journal Entries");
            cut.Markup.Should().NotContain("Bills");
            cut.Markup.Should().NotContain("Invoices");
            cut.Markup.Should().NotContain("System Overview");
        });
    }

    private void ConfigureAuthorization(ClaimsPrincipal user)
    {
        Services.AddAuthorizationCore(options =>
        {
            options.AddPolicy(ApplicationAuthorizationPolicies.RequireSuperAdmin, policy =>
                policy.RequireAuthenticatedUser()
                    .RequireAssertion(context => ApplicationAuthorizationScopeEvaluator.IsSuperAdmin(context.User)));

            options.AddPolicy(ApplicationAuthorizationPolicies.RequireTenantAccess, policy =>
                policy.RequireAuthenticatedUser()
                    .RequireAssertion(context => ApplicationAuthorizationScopeEvaluator.HasTenantAccess(context.User)));

            options.AddPolicy(ApplicationAuthorizationPolicies.RequireTenantOwner, policy =>
                policy.RequireAuthenticatedUser()
                    .RequireAssertion(context => ApplicationAuthorizationScopeEvaluator.IsTenantOwner(context.User)));

            options.AddPolicy(ApplicationAuthorizationPolicies.RequireTenantAccountingAccess, policy =>
                policy.RequireAuthenticatedUser()
                    .RequireAssertion(context => ApplicationAuthorizationScopeEvaluator.HasTenantAccountingAccess(context.User)));

            options.AddPolicy(ApplicationAuthorizationPolicies.RequireTenantOperationalAccess, policy =>
                policy.RequireAuthenticatedUser()
                    .RequireAssertion(context => ApplicationAuthorizationScopeEvaluator.HasTenantOperationalAccess(context.User)));
        });
        Services.RemoveAll<IAuthorizationService>();
        Services.AddScoped<IAuthorizationService, DefaultAuthorizationService>();
        Services.AddScoped<AuthenticationStateProvider>(_ => new StaticAuthenticationStateProvider(user));
    }

    private static ClaimsPrincipal CreatePrincipal(string role, int? companyId = null)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, "nav@test.com"),
            new(ClaimTypes.Role, role),
            new("role", role)
        };

        if (companyId.HasValue)
        {
            claims.Add(new Claim("CompanyId", companyId.Value.ToString()));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test", ClaimTypes.Name, ClaimTypes.Role));
    }

    private sealed class StaticAuthenticationStateProvider(ClaimsPrincipal user) : AuthenticationStateProvider
    {
        private readonly AuthenticationState _state = new(user);

        public override Task<AuthenticationState> GetAuthenticationStateAsync() => Task.FromResult(_state);
    }
}
