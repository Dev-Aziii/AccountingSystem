using AccountingSystem.Shared.DTOs;

namespace AccountingSystem.Client.Services
{
    public interface ILedgerService
    {
        Task<List<AccountDTO>> GetAccountsAsync();
        Task<JournalEntryDTO> PostJournalEntryAsync(JournalEntryDTO entry);
    }
}