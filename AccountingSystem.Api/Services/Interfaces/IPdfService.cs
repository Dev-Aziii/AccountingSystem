using AccountingSystem.Shared.DTOs;

namespace AccountingSystem.API.Services.Interfaces
{
    public interface IPdfService
    {
        byte[] GenerateInvoicePdf(InvoiceDTO invoice, CompanyDTO company, CustomerDTO customer);

        byte[] GenerateFinancialReportPdf(
            TrialBalanceDTO incomeTb,
            TrialBalanceDTO balanceTb,
            List<AccountDTO> accounts,
            CompanyDTO company,
            DateTime periodStart,
            DateTime periodEnd);
    }
}
