using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace AccountingSystem.API.Identity
{
    public class ApplicationUser : IdentityUser<int>
    {
        public int? LegacyUserId { get; set; }

        public int CompanyId { get; set; }

        [MaxLength(100)]
        public string FullName { get; set; } = string.Empty;

        [MaxLength(20)]
        public string Status { get; set; } = "Active";

        public bool IsActive { get; set; } = true;

        public bool IsDeleted { get; set; }

        public bool RequireEmailConfirmation { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }
    }
}
