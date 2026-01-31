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

        public async Task DownloadInvoicePdf(int invoiceId)
        {
            await _api.DownloadFileAsync($"api/reports/invoices/{invoiceId}/pdf", $"Invoice-{invoiceId}.pdf");
        }

        // NEW: Financials PDF
        public async Task DownloadFinancialsPdf()
        {
            await _api.DownloadFileAsync($"api/reports/financials/pdf", $"FinancialStatements.pdf");
        }
    }
}