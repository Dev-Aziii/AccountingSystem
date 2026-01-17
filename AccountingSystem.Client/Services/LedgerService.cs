using AccountingSystem.Shared.DTOs;
using System.Net.Http.Json;

namespace AccountingSystem.Client.Services
{
    public class LedgerService
    {
        private readonly ApiService _api;

        public LedgerService(ApiService api)
        {
            _api = api;
        }

        public async Task<List<AccountDTO>> GetAccountsAsync()
        {
            return await _api.GetAsync<List<AccountDTO>>("api/ledger/accounts");
        }

        public async Task<JournalEntryDTO> PostJournalEntryAsync(JournalEntryDTO entry)
        {
            var response = await _api.PostAsync("api/ledger/journal", entry);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception(error);
            }

            return await response.Content.ReadFromJsonAsync<JournalEntryDTO>();
        }
    }
}