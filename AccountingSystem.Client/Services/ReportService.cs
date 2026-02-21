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

        public async Task<TrialBalanceDTO?> GetTrialBalance(DateTime? from = null, DateTime? to = null, string view = "post")
        {
            var qs = $"api/ledger/trial-balance?view={view}";
            if (from.HasValue) qs += $"&from={from.Value:yyyy-MM-dd}";
            if (to.HasValue) qs += $"&to={to.Value:yyyy-MM-dd}";
            return await _api.GetAsync<TrialBalanceDTO>(qs);
        }

        public async Task DownloadInvoicePdf(int invoiceId)
        {
            await _api.DownloadFileAsync($"api/reports/invoices/{invoiceId}/pdf", $"Invoice-{invoiceId}.pdf");
        }

        // NEW: Financials PDF
        public async Task DownloadFinancialsPdf(DateTime? from = null, DateTime? to = null)
        {
            var qs = "api/reports/financials/pdf";
            var p = new List<string>();
            if (from.HasValue) p.Add($"from={from.Value:yyyy-MM-dd}");
            if (to.HasValue) p.Add($"to={to.Value:yyyy-MM-dd}");
            if (p.Any()) qs += "?" + string.Join("&", p);
            await _api.DownloadFileAsync(qs, $"FinancialStatements.pdf");
        }
    }
}