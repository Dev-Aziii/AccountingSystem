using AccountingSystem.API.Models;
using AccountingSystem.Shared.DTOs;
using AccountingSystem.Shared.Enums;

namespace AccountingSystem.API.Services.Interfaces
{
    public interface ILedgerService
    {
        Task<JournalEntry> CreateJournalEntryAsync(JournalEntryDTO entryDto, string userId, bool saveImmediately = true);
        Task<List<Account>> GetChartOfAccountsAsync(bool includeArchived = false);
        Task<TrialBalanceDTO> GetTrialBalanceAsync(DateTime? fromDate = null, DateTime? toDate = null, bool excludeClosingEntries = false);
        Task<Account> CreateAccountAsync(CreateAccountDTO dto);
        Task UpdateAccountAsync(int id, UpdateAccountDTO dto);
        Task DeleteAccountAsync(int id);
        Task RestoreAccountAsync(int id);
    }

    public interface IDocumentSequenceService
    {
        Task<string> GetNextSequenceAsync(int companyId, DocumentType documentType);
        Task<List<DocumentSequenceDTO>> GetSequencesAsync(int companyId);
        Task<DocumentSequenceDTO> UpsertSequenceAsync(int companyId, UpdateDocumentSequenceDTO dto);
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
        Task<List<VendorDTO>> GetVendorsAsync(bool includeArchived = false);
        Task<Vendor> CreateVendorAsync(CreateVendorDTO vendorDto);
        Task<Vendor> UpdateVendorAsync(int id, UpdateVendorDTO vendorDto);
        Task DeleteVendorAsync(int id);
        Task RestoreVendorAsync(int id);
        Task<List<BillDTO>> GetBillsAsync(int? fiscalYear = null, DocumentStatus? status = null);
        Task<Bill> CreateBillAsync(CreateBillDTO billDto);
        Task<PaymentCreationResultDTO> PayBillAsync(RecordPaymentDTO paymentDto, string userId);
    }

    public interface IReceivableService
    {
        Task<List<CustomerDTO>> GetCustomersAsync(bool includeArchived = false);
        Task<Customer> CreateCustomerAsync(CreateCustomerDTO customerDto);
        Task<Customer> UpdateCustomerAsync(int id, UpdateCustomerDTO customerDto);
        Task DeleteCustomerAsync(int id);
        Task RestoreCustomerAsync(int id);
        Task<List<InvoiceDTO>> GetInvoicesAsync(int? fiscalYear = null, DocumentStatus? status = null);
        Task<DocumentCreationResultDTO> CreateInvoiceAsync(CreateInvoiceDTO invoiceDto);
        Task<PaymentCreationResultDTO> ReceivePaymentAsync(RecordPaymentDTO paymentDto, string userId);
    }
}
