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
    }
}