using System.ComponentModel.DataAnnotations;

namespace AccountingSystem.Shared.DTOs
{
    public class CompanyDTO
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string TaxId { get; set; } = string.Empty;
        public string Currency { get; set; } = "PHP";
        public int FiscalYearStartMonth { get; set; } = 1;
    }

    public class UpdateCompanyDTO
    {
        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(200)]
        public string? Address { get; set; }

        [MaxLength(50)]
        public string? TaxId { get; set; }

        [MaxLength(10)]
        public string Currency { get; set; } = "PHP";

        [Range(1, 12)]
        public int FiscalYearStartMonth { get; set; } = 1;
    }
}
