using System.ComponentModel.DataAnnotations;

namespace AccountingSystem.API.Models
{
    public class Company
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(200)]
        public string? Address { get; set; }

        [MaxLength(50)]
        public string? TaxId { get; set; } // TIN

        [MaxLength(10)]
        public string Currency { get; set; } = "PHP";

        [Range(1, 12)]
        public int FiscalYearStartMonth { get; set; } = 1;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public bool IsActive { get; set; } = true;

        [MaxLength(20)]
        public string Status { get; set; } = "Active"; // Active, Suspended, Blocked
    }
}
