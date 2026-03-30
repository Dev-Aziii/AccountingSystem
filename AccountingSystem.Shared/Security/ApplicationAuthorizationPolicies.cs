namespace AccountingSystem.Shared.Security;

public static class ApplicationAuthorizationPolicies
{
    public const string RequireSuperAdmin = nameof(RequireSuperAdmin);
    public const string RequireTenantAccess = nameof(RequireTenantAccess);
    public const string RequireTenantOwner = nameof(RequireTenantOwner);
    public const string RequireTenantAccountingAccess = nameof(RequireTenantAccountingAccess);
    public const string RequireTenantOperationalAccess = nameof(RequireTenantOperationalAccess);
}
