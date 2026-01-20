using System.ComponentModel.DataAnnotations;

namespace AccountingSystem.API.Models
{
    public abstract class BaseEntity
    {
        [Key]
        public int Id { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        // ID of the user who created this record
        public int? CreatedById { get; set; }

        public bool IsActive { get; set; } = true;

        public bool IsDeleted { get; set; } = false; // Soft Delete
    }
}