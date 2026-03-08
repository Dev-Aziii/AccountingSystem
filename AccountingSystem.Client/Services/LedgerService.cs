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

        public async Task<List<AccountDTO>?> GetAccountsAsync(bool includeArchived = false)
        {
            return await _api.GetAsync<List<AccountDTO>>($"api/ledger/accounts?includeArchived={includeArchived}");
        }

        public async Task RestoreAccountAsync(int id)
        {
            var response = await _api.PutAsync<object?>($"api/ledger/accounts/{id}/restore", null);
            if (!response.IsSuccessStatusCode) throw new Exception(await response.Content.ReadAsStringAsync());
        }

        public async Task<AccountDTO?> CreateAccountAsync(CreateAccountDTO account)
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

        public async Task<JournalEntryDTO?> PostJournalEntryAsync(JournalEntryDTO entry)
        {
            var response = await _api.PostAsync("api/ledger/journal", entry);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception(error);
            }
            return await response.Content.ReadFromJsonAsync<JournalEntryDTO>();
        }

        public async Task<List<FiscalYearSummaryDTO>?> GetFiscalYearSummariesAsync(int lookbackYears = 10)
        {
            return await _api.GetAsync<List<FiscalYearSummaryDTO>>($"api/ledger/fiscal-years?lookbackYears={lookbackYears}");
        }

        public async Task<RunYearEndCloseResultDTO?> RunYearEndCloseAsync(int fiscalYear)
        {
            var response = await _api.PostAsync<object?>($"api/ledger/fiscal-years/{fiscalYear}/close", null);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception(error);
            }

            return await response.Content.ReadFromJsonAsync<RunYearEndCloseResultDTO>();
        }
    }
}
