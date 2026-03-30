using System;

namespace AccountingSystem.Shared.Security;

public static class ApplicationRoles
{
    public const string SuperAdmin = "SuperAdmin";
    public const string TenantOwner = "TenantOwner";
    public const string Accounting = "Accounting";
    public const string Management = "Management";

    public const string TenantOwnerAndAccounting = TenantOwner + "," + Accounting;
    public const string TenantOwnerAccountingAndManagement = TenantOwner + "," + Accounting + "," + Management;

    public static bool IsSuperAdmin(string? roleName) =>
        string.Equals(roleName, SuperAdmin, StringComparison.Ordinal);

    public static bool IsTenantOwner(string? roleName) =>
        string.Equals(roleName, TenantOwner, StringComparison.Ordinal);
}

public static class ApplicationRoleDisplayNames
{
    public const string SuperAdmin = "Super Admin";
    public const string TenantOwner = "Tenant Owner";
    public const string Accounting = ApplicationRoles.Accounting;
    public const string Management = ApplicationRoles.Management;

    public static string Get(string? roleName) =>
        roleName switch
        {
            ApplicationRoles.SuperAdmin => SuperAdmin,
            ApplicationRoles.TenantOwner => TenantOwner,
            ApplicationRoles.Accounting => Accounting,
            ApplicationRoles.Management => Management,
            _ => roleName ?? string.Empty
        };
}
