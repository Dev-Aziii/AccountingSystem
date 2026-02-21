using System.ComponentModel.DataAnnotations;

namespace AccountingSystem.Shared.DTOs
{
    public class FiscalYearDTO
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public bool IsClosed { get; set; }
        public DateTime? ClosedAt { get; set; }
        public int? ClosedByUserId { get; set; }
    }

    public class CreateFiscalYearDTO
    {
        [Required]
        public string Name { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
    }
}
