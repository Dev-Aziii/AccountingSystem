using AccountingSystem.Shared.Security;
using Microsoft.AspNetCore.Authorization;

namespace AccountingSystem.API.Configuration;

public static class ApplicationAuthorizationExtensions
{
    public static IServiceCollection AddApplicationAuthorization(this IServiceCollection services)
    {
        services.AddAuthorization(options =>
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

        return services;
    }
}
