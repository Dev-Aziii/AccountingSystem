using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace AccountingSystem.Shared.Security;

public static class ApplicationRoleAssignmentRules
{
    private static readonly ReadOnlyCollection<string> SuperAdminAssignableRoles = Array.AsReadOnly(new[]
    {
        ApplicationRoles.SuperAdmin,
        ApplicationRoles.TenantOwner,
        ApplicationRoles.Accounting,
        ApplicationRoles.Management
    });

    private static readonly ReadOnlyCollection<string> TenantOwnerAssignableRoles = Array.AsReadOnly(new[]
    {
        ApplicationRoles.Accounting,
        ApplicationRoles.Management
    });

    private static readonly ReadOnlyCollection<string> NoAssignableRoles = Array.AsReadOnly(Array.Empty<string>());

    public static IReadOnlyList<string> GetAssignableRoles(string? actorRole) =>
        actorRole switch
        {
            ApplicationRoles.SuperAdmin => SuperAdminAssignableRoles,
            ApplicationRoles.TenantOwner => TenantOwnerAssignableRoles,
            _ => NoAssignableRoles
        };

    public static bool CanAssignRole(string? actorRole, string? targetRole)
    {
        if (string.IsNullOrWhiteSpace(targetRole))
        {
            return false;
        }

        return GetAssignableRoles(actorRole)
            .Any(role => string.Equals(role, targetRole, StringComparison.Ordinal));
    }
}
