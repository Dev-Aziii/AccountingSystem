using AccountingSystem.Shared.Enums;

namespace AccountingSystem.Shared.DTOs
{
    public class SystemDashboardDTO
    {
        public int TotalCompanies { get; set; }
        public int ActiveCompanies { get; set; }
        public int SuspendedCompanies { get; set; }
        public int BlockedCompanies { get; set; }
        public int TotalUsers { get; set; }
        public int ActiveUsers { get; set; }
        public int RestrictedUsers { get; set; }
        public int BlockedUsers { get; set; }

        // Activity trends (last 12 months)
        public List<MonthlyActivityDTO> MonthlyRegistrations { get; set; } = new();
        public List<MonthlyActivityDTO> MonthlyUserGrowth { get; set; } = new();

        // Recent activity
        public List<SuperAdminAuditLogDTO> RecentActions { get; set; } = new();
    }

    public class MonthlyActivityDTO
    {
        public string Month { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    public class TenantDTO
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? Address { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsActive { get; set; }
        public string Status { get; set; } = "Active";
        public int UserCount { get; set; }
        public DateTime? LastActivityDate { get; set; }
    }

    public class GlobalUserDTO
    {
        public int Id { get; set; }
        public string? FullName { get; set; }
        public string? Email { get; set; }
        public string? Role { get; set; }
        public string? CompanyName { get; set; }
        public int CompanyId { get; set; }
        public bool IsActive { get; set; }
        public string Status { get; set; } = "Active";
        public DateTime CreatedAt { get; set; }
        public DateTime? LastLoginDate { get; set; }
    }

    public class UpdateCompanyStatusDTO
    {
        public string Status { get; set; } = string.Empty; // Active, Suspended, Blocked
    }

    public class UpdateUserStatusDTO
    {
        public string Status { get; set; } = string.Empty; // Active, Restricted, Blocked
    }

    // Super Admin Audit Log DTO
    public class SuperAdminAuditLogDTO
    {
        public int Id { get; set; }
        public string AdminEmail { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string TargetType { get; set; } = string.Empty; // "User" or "Company"
        public string TargetName { get; set; } = string.Empty;
        public int TargetId { get; set; }
        public string OldValue { get; set; } = string.Empty;
        public string NewValue { get; set; } = string.Empty;
        public string Details { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
    }
}