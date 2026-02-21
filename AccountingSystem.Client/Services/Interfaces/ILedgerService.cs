using AccountingSystem.Shared.DTOs;

namespace AccountingSystem.Client.Services.Interfaces
{
    public interface ILedgerService
    {
        Task<List<AccountDTO>?> GetAccountsAsync(bool includeArchived = false);
        Task<JournalEntryDTO?> PostJournalEntryAsync(JournalEntryDTO entry);
        Task<List<FiscalYearSummaryDTO>?> GetFiscalYearSummariesAsync(int lookbackYears = 10);
        Task<RunYearEndCloseResultDTO?> RunYearEndCloseAsync(int fiscalYear);
    }
}
