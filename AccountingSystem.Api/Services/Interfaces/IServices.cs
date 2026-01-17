using AccountingSystem.Shared.DTOs;
using AccountingSystem.API.Models;

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
        Task<Bill> CreateBillAsync(CreateBillDTO billDto);
        Task<Payment> PayBillAsync(int billId, decimal amount, string paymentMethod, string userId);
    }

    public interface IReceivableService
    {
        Task<Invoice> CreateInvoiceAsync(CreateInvoiceDTO invoiceDto);
        Task<Payment> ReceivePaymentAsync(int invoiceId, decimal amount, string paymentMethod, string userId);
    }
}