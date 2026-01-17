using System.ComponentModel.DataAnnotations;

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
        public decimal Amount { get; set; }
        public decimal AmountPaid { get; set; }
        public string ReferenceNumber { get; set; }
        public bool IsPaid { get; set; } = false;
    }

    // --- ACCOUNTS RECEIVABLE ---
    public class Invoice
    {
        public int Id { get; set; }
        public int CustomerId { get; set; }
        public Customer Customer { get; set; }

        public DateTime DueDate { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal PaidAmount { get; set; }
        public string Status { get; set; } = "Unpaid"; // Unpaid, Paid, Overdue
    }

    // --- TRANSACTIONS ---
    public class Payment
    {
        public int Id { get; set; }
        public DateTime Date { get; set; }
        public decimal Amount { get; set; }

        public string PaymentMethod { get; set; } // Cash, Bank, PayMongo
        public string ReferenceId { get; set; } // PayMongo ID
        public string Type { get; set; } // Incoming (AR), Outgoing (AP)

        public int? InvoiceId { get; set; } // Linked AR
        public int? BillId { get; set; }    // Linked AP
    }

    // --- SECURITY ---
    public class AuditLog
    {
        public int Id { get; set; }
        public string UserId { get; set; }
        public string Action { get; set; } // POST, PUT, DELETE
        public string EntityName { get; set; } // JournalEntry, Invoice
        public string EntityId { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string Changes { get; set; } // JSON Payload
    }
}