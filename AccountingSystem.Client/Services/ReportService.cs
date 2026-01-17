using AccountingSystem.Shared.DTOs;

namespace AccountingSystem.Client.Services
{
    public class ReportService
    {
        private readonly ApiService _api;

        public ReportService(ApiService api)
        {
            _api = api;
        }

        public async Task<TrialBalanceDTO> GetTrialBalance()
        {
            return await _api.GetAsync<TrialBalanceDTO>("api/ledger/trial-balance");
        }
    }
}