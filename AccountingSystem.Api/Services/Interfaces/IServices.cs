using AccountingSystem.API.Models;
using AccountingSystem.Shared.DTOs;
using AccountingSystem.Shared.Enums;

namespace AccountingSystem.API.Services.Interfaces
{
    public interface ILedgerService
    {
        Task<JournalEntry> CreateJournalEntryAsync(JournalEntryDTO entryDto, string userId);
        Task<List<Account>> GetChartOfAccountsAsync(bool includeArchived = false); // Updated
        Task<TrialBalanceDTO> GetTrialBalanceAsync(
            DateTime? fromDate = null,
            DateTime? toDate = null,
            bool excludeClosingEntries = false);

        // Account Management
        Task<Account> CreateAccountAsync(CreateAccountDTO dto);
        Task UpdateAccountAsync(int id, UpdateAccountDTO dto);
        Task DeleteAccountAsync(int id);
        Task RestoreAccountAsync(int id); // New
    }

    public interface IYearEndCloseService
    {
        Task<List<FiscalYearSummaryDTO>> GetFiscalYearSummariesAsync(int lookbackYears = 10);
        Task<RunYearEndCloseResultDTO> CloseFiscalYearAsync(int fiscalYear, string userName);
        Task EnsurePostingDateIsOpenAsync(DateTime postingDate);
        FiscalPeriod ResolveFiscalPeriod(int fiscalYear);
    }

    public record FiscalPeriod(int FiscalYear, DateTime StartDate, DateTime EndDate);

    public interface IPayableService
    {
        Task<List<VendorDTO>> GetVendorsAsync(bool includeArchived = false); // Updated

        // Vendor CRUD
        Task<Vendor> CreateVendorAsync(CreateVendorDTO vendorDto);
        Task<Vendor> UpdateVendorAsync(int id, UpdateVendorDTO vendorDto);
        Task DeleteVendorAsync(int id);
        Task RestoreVendorAsync(int id); // New

        // Bills
        Task<List<BillDTO>> GetBillsAsync(int? fiscalYear = null, DocumentStatus? status = null);
        Task<Bill> CreateBillAsync(CreateBillDTO billDto);
        Task<Payment> PayBillAsync(RecordPaymentDTO paymentDto, string userId);
    }

    public interface IReceivableService
    {
        Task<List<CustomerDTO>> GetCustomersAsync(bool includeArchived = false); // Updated

        // Customer CRUD
        Task<Customer> CreateCustomerAsync(CreateCustomerDTO customerDto);
        Task<Customer> UpdateCustomerAsync(int id, UpdateCustomerDTO customerDto);
        Task DeleteCustomerAsync(int id);
        Task RestoreCustomerAsync(int id); // New

        // Invoices
        Task<List<InvoiceDTO>> GetInvoicesAsync(int? fiscalYear = null, DocumentStatus? status = null);
        Task<Invoice> CreateInvoiceAsync(CreateInvoiceDTO invoiceDto);
        Task<Payment> ReceivePaymentAsync(RecordPaymentDTO paymentDto, string userId);
    }
}
