using AccountingSystem.Shared.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace AccountingSystem.API.Models
{
    // --- LEDGER ENTITIES ---
    public class Account : BaseEntity
    {
        [Required]
        [MaxLength(20)]
        public string Code { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [MaxLength(20)]
        public string Type { get; set; } = string.Empty;

        [Column(TypeName = "decimal(18,2)")]
        public decimal Balance { get; set; } = 0;
    }

    public class JournalEntry : BaseEntity
    {
        public DateTime Date { get; set; }

        [MaxLength(500)]
        public string Description { get; set; } = string.Empty;

        [MaxLength(50)]
        public string Reference { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? CreatedBy { get; set; }

        public bool IsPosted { get; set; } = false;

        // Navigation
        public List<JournalEntryLine> Lines { get; set; } = new List<JournalEntryLine>();
    }

    public class JournalEntryLine
    {
        public int Id { get; set; }

        public int JournalEntryId { get; set; }

        [JsonIgnore]
        public JournalEntry JournalEntry { get; set; } = default!;

        public int AccountId { get; set; }
        public Account Account { get; set; } = default!;

        [Column(TypeName = "decimal(18,2)")]
        public decimal Debit { get; set; } = 0;

        [Column(TypeName = "decimal(18,2)")]
        public decimal Credit { get; set; } = 0;
    }

    public class FiscalYearClose : BaseEntity
    {
        public int FiscalYear { get; set; }

        public DateTime PeriodStart { get; set; }

        public DateTime PeriodEnd { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal NetIncome { get; set; }

        public int ClosingJournalEntryId { get; set; }
        public JournalEntry ClosingJournalEntry { get; set; } = default!;

        public DateTime ClosedAtUtc { get; set; }

        [MaxLength(100)]
        public string? ClosedBy { get; set; }
    }

    // --- PARTNERS ---
    public class Vendor : BaseEntity
    {
        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [EmailAddress]
        [MaxLength(100)]
        public string? Email { get; set; }

        [MaxLength(100)]
        public string? ContactPerson { get; set; }

        [Phone]
        [MaxLength(20)]
        public string? Phone { get; set; }

        public virtual ICollection<Bill> Bills { get; set; } = new List<Bill>();
    }

    public class Customer : BaseEntity
    {
        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [EmailAddress]
        [MaxLength(100)]
        public string? Email { get; set; }

        [Phone]
        [MaxLength(20)]
        public string? Phone { get; set; }

        public virtual ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
    }

    // --- ACCOUNTS PAYABLE ---
    public class Bill : BaseEntity
    {
        public int VendorId { get; set; }
        public virtual Vendor Vendor { get; set; } = default!;

        public DateTime DueDate { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal AmountPaid { get; set; }

        [MaxLength(50)]
        public string VendorReferenceNumber { get; set; } = string.Empty;

        [MaxLength(50)]
        public string SystemReferenceNumber { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Description { get; set; }

        public DocumentStatus Status { get; set; } = DocumentStatus.Unpaid;

        [NotMapped]
        public decimal Balance => Amount - AmountPaid;
    }

    // --- ACCOUNTS RECEIVABLE ---
    public class Invoice : BaseEntity
    {
        public int CustomerId { get; set; }
        public virtual Customer Customer { get; set; } = default!;

        public DateTime DueDate { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal PaidAmount { get; set; }

        [MaxLength(50)]
        public string ReferenceNumber { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Description { get; set; }

        public DocumentStatus Status { get; set; } = DocumentStatus.Unpaid;

        [NotMapped]
        public decimal Balance => TotalAmount - PaidAmount;
    }

    // --- TRANSACTIONS ---
    public class Payment : BaseEntity
    {
        public DateTime Date { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        public PaymentMethod PaymentMethod { get; set; }
        public PaymentType Type { get; set; }

        [MaxLength(50)]
        public string? ReferenceNumber { get; set; }

        [MaxLength(500)]
        public string? Remarks { get; set; }

        public int? AccountId { get; set; }
        public Account? Account { get; set; }

        public int? InvoiceId { get; set; }
        public virtual Invoice? Invoice { get; set; }

        public int? BillId { get; set; }
        public virtual Bill? Bill { get; set; }
    }



    public class DocumentSequence
    {
        public int Id { get; set; }
        public int CompanyId { get; set; }
        public DocumentType DocumentType { get; set; }

        [MaxLength(20)]
        public string Prefix { get; set; } = string.Empty;

        public int NextNumber { get; set; } = 1;

        [Timestamp]
        public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    }

    // --- SECURITY ---
    public class AuditLog
    {
        public int Id { get; set; }

        // NEW: Multi-Tenancy field
        public int CompanyId { get; set; }

        public int? UserId { get; set; }
        public string Action { get; set; } = string.Empty;
        public string EntityName { get; set; } = string.Empty;
        public string EntityId { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string Changes { get; set; } = string.Empty;
    }
}
