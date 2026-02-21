namespace AccountingSystem.Shared.DTOs
{
    public class FiscalYearSummaryDTO
    {
        public int FiscalYear { get; set; }
        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }
        public decimal NetIncome { get; set; }
        public bool HasActivity { get; set; }
        public bool IsClosed { get; set; }
        public DateTime? ClosedAtUtc { get; set; }
        public bool CanClose { get; set; }
    }

    public class RunYearEndCloseRequestDTO
    {
        public int FiscalYear { get; set; }
    }

    public class RunYearEndCloseResultDTO
    {
        public int FiscalYear { get; set; }
        public int ClosingJournalEntryId { get; set; }
        public decimal NetIncome { get; set; }
        public DateTime ClosedAtUtc { get; set; }
    }
}
