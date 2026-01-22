using System.ComponentModel.DataAnnotations;

namespace AccountingSystem.API.Models
{
    public abstract class BaseEntity
    {
        [Key]
        public int Id { get; set; }

        // --- Multi-Tenancy Field ---
        public int CompanyId { get; set; }
        // ---------------------------

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        public int? CreatedById { get; set; }

        public bool IsActive { get; set; } = true;

        public bool IsDeleted { get; set; } = false;
    }
}