using System.ComponentModel.DataAnnotations;

namespace AccountingSystem.Shared.DTOs
{
    // --- LEDGER ---
    public class JournalEntryDTO
    {
        public string Description { get; set; }
        public string Reference { get; set; }
        public DateTime Date { get; set; }
        public List<JournalEntryLineDTO> Lines { get; set; }
    }

    public class JournalEntryLineDTO
    {
        public int AccountId { get; set; }
        public decimal Debit { get; set; }
        public decimal Credit { get; set; }
    }

    public class TrialBalanceDTO
    {
        public DateTime GeneratedAt { get; set; }
        public List<AccountBalanceDTO> Accounts { get; set; }
        public decimal TotalDebit { get; set; }
        public decimal TotalCredit { get; set; }
    }

    public class AccountBalanceDTO
    {
        public string AccountName { get; set; }
        public string AccountCode { get; set; }
        public decimal Debit { get; set; }
        public decimal Credit { get; set; }
    }

    // --- PAYABLES ---
    public class CreateBillDTO
    {
        public int VendorId { get; set; }
        public DateTime DueDate { get; set; }
        public decimal Amount { get; set; }
        public string ReferenceNumber { get; set; }
        public string Description { get; set; } // For GL
        public int ExpenseAccountId { get; set; } // Where to debit expense
    }

    // --- RECEIVABLES ---
    public class CreateInvoiceDTO
    {
        public int CustomerId { get; set; }
        public DateTime DueDate { get; set; }
        public decimal Amount { get; set; }
        public string Description { get; set; }
        public int RevenueAccountId { get; set; } // Where to credit income
    }

    // --- PAYMENTS ---
    public class ProcessPaymentDTO
    {
        public decimal Amount { get; set; }
        public string PaymentMethod { get; set; } // "Cash", "Bank"
    }
}
namespace AccountingSystem.Api.DTOs
{
    public class CoreDTOs
    {
    }
}
