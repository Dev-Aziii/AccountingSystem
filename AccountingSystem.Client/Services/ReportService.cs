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

        public async Task<TrialBalanceDTO?> GetTrialBalance(
            DateTime? fromDate = null,
            DateTime? toDate = null,
            bool excludeClosingEntries = false)
        {
            var queryParams = new List<string>();
            if (fromDate.HasValue)
                queryParams.Add($"fromDate={fromDate.Value:yyyy-MM-dd}");
            if (toDate.HasValue)
                queryParams.Add($"toDate={toDate.Value:yyyy-MM-dd}");
            if (excludeClosingEntries)
                queryParams.Add("excludeClosingEntries=true");

            var query = queryParams.Count > 0 ? "?" + string.Join("&", queryParams) : string.Empty;
            return await _api.GetAsync<TrialBalanceDTO>($"api/ledger/trial-balance{query}");
        }

        public async Task DownloadInvoicePdf(int invoiceId)
        {
            await _api.DownloadFileAsync($"api/reports/invoices/{invoiceId}/pdf", $"Invoice-{invoiceId}.pdf");
        }

        public async Task DownloadFinancialsPdf(int fiscalYear)
        {
            await _api.DownloadFileAsync($"api/reports/financials/pdf?fiscalYear={fiscalYear}", $"FinancialStatements-FY{fiscalYear}.pdf");
        }
    }
}
