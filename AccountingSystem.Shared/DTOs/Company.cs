using System.ComponentModel.DataAnnotations;

namespace AccountingSystem.Shared.DTOs
{
    public class CompanyDTO
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Address { get; set; }
        public string TaxId { get; set; }
        public string Currency { get; set; }
    }

    public class UpdateCompanyDTO
    {
        [Required]
        [MaxLength(100)]
        public string Name { get; set; }

        [MaxLength(200)]
        public string? Address { get; set; }

        [MaxLength(50)]
        public string? TaxId { get; set; }

        [MaxLength(10)]
        public string Currency { get; set; } = "PHP";
    }
}