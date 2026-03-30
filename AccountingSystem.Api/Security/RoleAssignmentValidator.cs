using System.Security.Claims;
using AccountingSystem.API.Models;
using AccountingSystem.Shared.Security;

namespace AccountingSystem.API.Security;

public readonly record struct TenantActorScope(string ActorRole, int TenantId);

public static class RoleAssignmentValidator
{
    public static bool TryGetTenantOwnerScope(ClaimsPrincipal? actor, out int tenantId)
    {
        tenantId = 0;

        return ApplicationAuthorizationScopeEvaluator.IsTenantOwner(actor)
            && ApplicationAuthorizationScopeEvaluator.TryGetCompanyId(actor, out tenantId);
    }

    public static TenantActorScope RequireTenantOwnerScope(ClaimsPrincipal? actor)
    {
        if (!ApplicationAuthorizationScopeEvaluator.IsAuthenticated(actor))
        {
            throw new InvalidOperationException("Authenticated user context is required.");
        }

        var actorRole = ApplicationAuthorizationScopeEvaluator.GetRole(actor);
        if (!ApplicationRoles.IsTenantOwner(actorRole))
        {
            throw new InvalidOperationException("Only tenant owners can manage users from this endpoint.");
        }

        if (!ApplicationAuthorizationScopeEvaluator.TryGetCompanyId(actor, out var tenantId))
        {
            throw new InvalidOperationException("User management requires a valid tenant company context.");
        }

        return new TenantActorScope(actorRole!, tenantId);
    }

    public static void EnsureCanAssignRole(string actorRole, string targetRole)
    {
        if (!ApplicationRoleAssignmentRules.CanAssignRole(actorRole, targetRole))
        {
            throw new InvalidOperationException(
                $"Role '{ApplicationRoleDisplayNames.Get(targetRole)}' cannot be assigned by {ApplicationRoleDisplayNames.Get(actorRole)}.");
        }
    }

    public static void EnsureTenantManagedUser(User user)
    {
        if (user.Role == null)
        {
            throw new InvalidOperationException("User role information is required.");
        }

        if (ApplicationRoles.IsSuperAdmin(user.Role.Name))
        {
            throw new InvalidOperationException("Super Admin accounts cannot be managed from tenant user management.");
        }

        if (ApplicationRoles.IsTenantOwner(user.Role.Name))
        {
            throw new InvalidOperationException("Tenant owner accounts can only be managed by Super Admin.");
        }
    }
}
