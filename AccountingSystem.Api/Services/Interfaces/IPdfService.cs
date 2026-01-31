using AccountingSystem.Shared.DTOs;

namespace AccountingSystem.API.Services.Interfaces
{
    public interface IPdfService
    {
        byte[] GenerateInvoicePdf(InvoiceDTO invoice, CompanyDTO company, CustomerDTO customer);

        // NEW: Financial Reports
        byte[] GenerateFinancialReportPdf(TrialBalanceDTO tb, List<AccountDTO> accounts, CompanyDTO company);
    }
}