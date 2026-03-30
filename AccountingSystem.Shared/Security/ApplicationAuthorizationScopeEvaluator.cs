using System.Security.Claims;

namespace AccountingSystem.Shared.Security;

public static class ApplicationAuthorizationScopeEvaluator
{
    private const string CompanyIdClaimType = "CompanyId";

    public static bool IsAuthenticated(ClaimsPrincipal? user) =>
        user?.Identity?.IsAuthenticated == true;

    public static bool IsSuperAdmin(ClaimsPrincipal? user) =>
        IsAuthenticated(user) && HasRole(user, ApplicationRoles.SuperAdmin);

    public static bool IsTenantOwner(ClaimsPrincipal? user) =>
        HasTenantContext(user) && HasRole(user, ApplicationRoles.TenantOwner);

    public static bool HasTenantAccess(ClaimsPrincipal? user) =>
        HasTenantContext(user) && HasAnyRole(user, ApplicationRoles.TenantOwner, ApplicationRoles.Accounting, ApplicationRoles.Management);

    public static bool HasTenantAccountingAccess(ClaimsPrincipal? user) =>
        HasTenantContext(user) && HasAnyRole(user, ApplicationRoles.TenantOwner, ApplicationRoles.Accounting);

    public static bool HasTenantOperationalAccess(ClaimsPrincipal? user) =>
        HasTenantContext(user) && HasAnyRole(user, ApplicationRoles.TenantOwner, ApplicationRoles.Accounting, ApplicationRoles.Management);

    public static bool IsTenantScopedPrincipal(ClaimsPrincipal? user) =>
        IsAuthenticated(user) && HasAnyRole(user, ApplicationRoles.TenantOwner, ApplicationRoles.Accounting, ApplicationRoles.Management);

    public static bool HasTenantContext(ClaimsPrincipal? user) =>
        IsAuthenticated(user) && TryGetCompanyId(user, out _);

    public static bool TryGetCompanyId(ClaimsPrincipal? user, out int companyId)
    {
        companyId = 0;
        if (!IsAuthenticated(user))
        {
            return false;
        }

        var claimValue = user!.Claims.FirstOrDefault(c => string.Equals(c.Type, CompanyIdClaimType, StringComparison.Ordinal))?.Value;
        return int.TryParse(claimValue, out companyId) && companyId > 0;
    }

    public static string? GetRole(ClaimsPrincipal? user)
    {
        if (!IsAuthenticated(user))
        {
            return null;
        }

        return user!.Claims
            .FirstOrDefault(c =>
                string.Equals(c.Type, ClaimTypes.Role, StringComparison.Ordinal) ||
                string.Equals(c.Type, "role", StringComparison.Ordinal))
            ?.Value;
    }

    public static bool HasRole(ClaimsPrincipal? user, string roleName)
    {
        return IsAuthenticated(user) && user!.Claims.Any(c =>
            (string.Equals(c.Type, ClaimTypes.Role, StringComparison.Ordinal) ||
             string.Equals(c.Type, "role", StringComparison.Ordinal)) &&
            string.Equals(c.Value, roleName, StringComparison.Ordinal));
    }

    public static bool HasAnyRole(ClaimsPrincipal? user, params string[] roleNames)
    {
        if (!IsAuthenticated(user))
        {
            return false;
        }

        foreach (var roleName in roleNames)
        {
            if (HasRole(user, roleName))
            {
                return true;
            }
        }

        return false;
    }
}
