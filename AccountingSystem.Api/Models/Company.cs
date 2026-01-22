using System.ComponentModel.DataAnnotations;

namespace AccountingSystem.API.Models
{
    public class Company
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; }

        [MaxLength(200)]
        public string? Address { get; set; }

        [MaxLength(50)]
        public string? TaxId { get; set; } // TIN

        [MaxLength(10)]
        public string Currency { get; set; } = "PHP";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public bool IsActive { get; set; } = true;
    }
}