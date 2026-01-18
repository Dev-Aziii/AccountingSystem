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

        // --- NEW CRUD METHODS ---
        public async Task<AccountDTO> CreateAccountAsync(CreateAccountDTO account)
        {
            var response = await _api.PostAsync("api/ledger/accounts", account);
            if (!response.IsSuccessStatusCode) throw new Exception(await response.Content.ReadAsStringAsync());

            return await response.Content.ReadFromJsonAsync<AccountDTO>();
        }

        public async Task UpdateAccountAsync(UpdateAccountDTO account)
        {
            var response = await _api.PutAsync($"api/ledger/accounts/{account.Id}", account);
            if (!response.IsSuccessStatusCode) throw new Exception(await response.Content.ReadAsStringAsync());
        }

        public async Task DeleteAccountAsync(int id)
        {
            var response = await _api.DeleteAsync($"api/ledger/accounts/{id}");
            if (!response.IsSuccessStatusCode) throw new Exception(await response.Content.ReadAsStringAsync());
        }
        // ------------------------

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