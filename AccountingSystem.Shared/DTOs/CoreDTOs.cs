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

    public class BillDTO
    {
        public int Id { get; set; }
        public int VendorId { get; set; }
        public string VendorName { get; set; }
        public DateTime DueDate { get; set; }
        public decimal Amount { get; set; }
        public string ReferenceNumber { get; set; }
        public string Description { get; set; }
        public decimal AmountPaid { get; set; }
        public string Status { get; set; }
        public decimal Balance => Amount - AmountPaid;
    }
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
    public class InvoiceDTO
    {
        public int Id { get; set; }
        public int CustomerId { get; set; }
        public string CustomerName { get; set; }
        public DateTime DueDate { get; set; }
        public decimal TotalAmount { get; set; }
        public string Description { get; set; }
        public decimal PaidAmount { get; set; }
        public string Status { get; set; }
        public decimal Balance => TotalAmount - PaidAmount;
    }
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
