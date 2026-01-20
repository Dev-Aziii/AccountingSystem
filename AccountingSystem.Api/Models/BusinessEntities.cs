using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AccountingSystem.API.Models
{
    // --- PARTNERS ---
    public class Vendor
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
        public string ContactPerson { get; set; }
    }

    public class Customer
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
    }

    // --- ACCOUNTS PAYABLE ---
    public class Bill
    {
        public int Id { get; set; }
        public int VendorId { get; set; }
        public Vendor Vendor { get; set; }

        public DateTime DueDate { get; set; }
        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; } // Original Total

        public string ReferenceNumber { get; set; }
        public string Description { get; set; }

        // Payment Tracking
        [Column(TypeName = "decimal(18,2)")]
        public decimal AmountPaid { get; set; } = 0;
        public string Status { get; set; } = "Unpaid"; // Unpaid, Partial, Paid

        [NotMapped]
        public decimal Balance => Amount - AmountPaid;
    }

    // --- ACCOUNTS RECEIVABLE ---
    public class Invoice
    {
        public int Id { get; set; }
        public int CustomerId { get; set; }
        public Customer Customer { get; set; }

        public DateTime DueDate { get; set; }
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalAmount { get; set; } // Original Total

        public string Description { get; set; }

        // Payment Tracking
        [Column(TypeName = "decimal(18,2)")]
        public decimal PaidAmount { get; set; } = 0;
        public string Status { get; set; } = "Unpaid"; // Unpaid, Partial, Paid

        [NotMapped]
        public decimal Balance => TotalAmount - PaidAmount;
    }

    // --- TRANSACTIONS ---
    public class Payment
    {
        public int Id { get; set; }
        public DateTime Date { get; set; }
        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        public string PaymentMethod { get; set; } // Cash, Check, Online
        public string ReferenceNumber { get; set; } // Check # or PayMongo Ref
        public string Remarks { get; set; }

        public string Type { get; set; } // "Incoming" (AR) or "Outgoing" (AP)

        // GL Integration: Which Asset Account was hit? (e.g., Cash on Hand)
        public int? AccountId { get; set; }
        public Account Account { get; set; }

        // Links
        public int? InvoiceId { get; set; } // For AR
        public Invoice Invoice { get; set; }

        public int? BillId { get; set; }    // For AP
        public Bill Bill { get; set; }
    }

    // --- SECURITY ---
    public class AuditLog
    {
        public int Id { get; set; }
        public string UserId { get; set; }
        public string Action { get; set; }
        public string EntityName { get; set; }
        public string EntityId { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string Changes { get; set; }
    }
}