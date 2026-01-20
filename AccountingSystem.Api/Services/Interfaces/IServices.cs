using AccountingSystem.API.Models;
using AccountingSystem.Shared.DTOs;

namespace AccountingSystem.API.Services.Interfaces
{
    public interface ILedgerService
    {
        Task<JournalEntry> CreateJournalEntryAsync(JournalEntryDTO entryDto, string userId);
        Task<List<Account>> GetChartOfAccountsAsync();
        Task<TrialBalanceDTO> GetTrialBalanceAsync();
    }

    public interface IPayableService
    {
        Task<List<VendorDTO>> GetVendorsAsync();
        Task<Bill> CreateBillAsync(CreateBillDTO billDto);
        Task<Payment> PayBillAsync(RecordPaymentDTO paymentDto, string userId);
    }

    public interface IReceivableService
    {
        Task<List<CustomerDTO>> GetCustomersAsync();
        Task<Invoice> CreateInvoiceAsync(CreateInvoiceDTO invoiceDto);
        Task<Payment> ReceivePaymentAsync(RecordPaymentDTO paymentDto, string userId);
    }
}