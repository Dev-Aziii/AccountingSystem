using AccountingSystem.Shared.Enums;
using System.ComponentModel.DataAnnotations;

namespace AccountingSystem.Shared.DTOs
{
    // --- LEDGER ---
    public class JournalEntryDTO
    {
        public string Description { get; set; } = string.Empty;
        public string Reference { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public List<JournalEntryLineDTO> Lines { get; set; } = new();
        public bool IsSystemGenerated { get; set; }
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
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }
        public List<AccountBalanceDTO> Accounts { get; set; } = new();
        public decimal TotalDebit { get; set; }
        public decimal TotalCredit { get; set; }
    }

    public class AccountBalanceDTO
    {
        public string AccountName { get; set; } = string.Empty;
        public string AccountCode { get; set; } = string.Empty;
        public decimal Debit { get; set; }
        public decimal Credit { get; set; }
    }

    // --- PAYABLES ---
    public class BillDTO
    {
        public int Id { get; set; }
        public int VendorId { get; set; }
        public string VendorName { get; set; } = string.Empty;
        public DateTime DueDate { get; set; }
        public decimal Amount { get; set; }
        public string ReferenceNumber { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal AmountPaid { get; set; }
        public DocumentStatus Status { get; set; } // Enum
        public decimal Balance => Amount - AmountPaid;
    }

    public class CreateBillDTO
    {
        public int VendorId { get; set; }
        public DateTime DueDate { get; set; }
        public decimal Amount { get; set; }
        public string ReferenceNumber { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int ExpenseAccountId { get; set; }
    }

    // --- RECEIVABLES ---
    public class InvoiceDTO
    {
        public int Id { get; set; }
        public int CustomerId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public DateTime DueDate { get; set; }
        public decimal TotalAmount { get; set; }
        public string Description { get; set; } = string.Empty;
        public decimal PaidAmount { get; set; }
        public DocumentStatus Status { get; set; } // Enum
        public decimal Balance => TotalAmount - PaidAmount;
    }

    public class CreateInvoiceDTO
    {
        public int CustomerId { get; set; }
        public DateTime DueDate { get; set; }
        public decimal Amount { get; set; }
        public string Description { get; set; } = string.Empty;
        public int RevenueAccountId { get; set; }
    }
}