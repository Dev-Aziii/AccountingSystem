using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace AccountingSystem.API.Models
{
    public class Role
    {
        public int Id { get; set; }
        [Required]
        [MaxLength(50)]
        public string Name { get; set; } = string.Empty; // Admin, Accounting, Management
    }

    public class User : BaseEntity
    {
        [Required]
        [EmailAddress]
        [MaxLength(100)]
        public string Email { get; set; } = string.Empty; // Replaces Username

        [JsonIgnore]
        public string PasswordHash { get; set; } = string.Empty;

        [JsonIgnore]
        public string? PasswordSalt { get; set; } // Added for security

        [MaxLength(100)]
        public string FullName { get; set; } = string.Empty;

        public int RoleId { get; set; }
        public virtual Role Role { get; set; } = null!;

        [MaxLength(20)]
        public string Status { get; set; } = "Active"; // Active, Restricted, Blocked

        public int AccessFailedCount { get; set; }

        public DateTime? LockoutEndUtc { get; set; }
    }

    // Super Admin Audit Log - separate from tenant audit logs
    public class SuperAdminAuditLog
    {
        [Key]
        public int Id { get; set; }

        public int AdminUserId { get; set; }

        [MaxLength(100)]
        public string AdminEmail { get; set; } = string.Empty;

        [MaxLength(100)]
        public string Action { get; set; } = string.Empty; // e.g., "USER_STATUS_CHANGE", "COMPANY_STATUS_CHANGE"

        [MaxLength(50)]
        public string TargetType { get; set; } = string.Empty; // "User" or "Company"

        public int TargetId { get; set; }

        [MaxLength(200)]
        public string TargetName { get; set; } = string.Empty;

        [MaxLength(50)]
        public string OldValue { get; set; } = string.Empty;

        [MaxLength(50)]
        public string NewValue { get; set; } = string.Empty;

        [MaxLength(500)]
        public string Details { get; set; } = string.Empty;

        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
