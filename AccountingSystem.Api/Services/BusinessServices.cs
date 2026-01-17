using AccountingSystem.API.Data;
using AccountingSystem.Shared.DTOs;
using AccountingSystem.API.Models;
using AccountingSystem.API.Services.Interfaces;

namespace AccountingSystem.API.Services
{
    public class PayableService : IPayableService
    {
        private readonly AccountingDbContext _context;
        private readonly ILedgerService _ledgerService;

        public PayableService(AccountingDbContext context, ILedgerService ledgerService)
        {
            _context = context;
            _ledgerService = ledgerService;
        }

        public async Task<Bill> CreateBillAsync(CreateBillDTO billDto)
        {
            // 1. Create Bill Record
            var bill = new Bill
            {
                VendorId = billDto.VendorId,
                DueDate = billDto.DueDate,
                Amount = billDto.Amount,
                ReferenceNumber = billDto.ReferenceNumber
            };
            _context.Bills.Add(bill);

            // 2. Post to GL: Dr Expense, Cr Accounts Payable (2000 - Placeholder ID)
            // Note: In real app, fetch AP Account ID from settings
            var entry = new JournalEntryDTO
            {
                Date = DateTime.UtcNow,
                Description = $"Bill #{billDto.ReferenceNumber}: {billDto.Description}",
                Reference = billDto.ReferenceNumber,
                Lines = new List<JournalEntryLineDTO>
                {
                    new JournalEntryLineDTO { AccountId = billDto.ExpenseAccountId, Debit = billDto.Amount, Credit = 0 },
                    new JournalEntryLineDTO { AccountId = 2000, Debit = 0, Credit = billDto.Amount } // 2000 = AP
                }
            };

            // Assuming "System" user for auto-generated entries
            await _ledgerService.CreateJournalEntryAsync(entry, "System");
            await _context.SaveChangesAsync();

            return bill;
        }

        public async Task<Payment> PayBillAsync(int billId, decimal amount, string paymentMethod, string userId)
        {
            var bill = await _context.Bills.FindAsync(billId);
            if (bill == null) throw new Exception("Bill not found");

            // 1. Record Payment
            bill.AmountPaid += amount;
            if (bill.AmountPaid >= bill.Amount) bill.IsPaid = true;

            var payment = new Payment
            {
                BillId = billId,
                Amount = amount,
                Date = DateTime.UtcNow,
                PaymentMethod = paymentMethod,
                Type = "Outgoing"
            };
            _context.Payments.Add(payment);

            // 2. Post to GL: Dr Accounts Payable, Cr Cash/Bank
            var entry = new JournalEntryDTO
            {
                Date = DateTime.UtcNow,
                Description = $"Payment for Bill #{bill.ReferenceNumber}",
                Reference = $"PAY-{payment.Id}",
                Lines = new List<JournalEntryLineDTO>
                {
                    new JournalEntryLineDTO { AccountId = 2000, Debit = amount, Credit = 0 }, // Dr AP
                    new JournalEntryLineDTO { AccountId = 1000, Debit = 0, Credit = amount }  // Cr Cash (Placeholder 1000)
                }
            };

            await _ledgerService.CreateJournalEntryAsync(entry, userId);
            await _context.SaveChangesAsync();
            return payment;
        }
    }

    public class ReceivableService : IReceivableService
    {
        private readonly AccountingDbContext _context;
        private readonly ILedgerService _ledgerService;

        public ReceivableService(AccountingDbContext context, ILedgerService ledgerService)
        {
            _context = context;
            _ledgerService = ledgerService;
        }

        public async Task<Invoice> CreateInvoiceAsync(CreateInvoiceDTO invoiceDto)
        {
            var invoice = new Invoice
            {
                CustomerId = invoiceDto.CustomerId,
                DueDate = invoiceDto.DueDate,
                TotalAmount = invoiceDto.Amount
            };
            _context.Invoices.Add(invoice);

            // GL: Dr Accounts Receivable (1100), Cr Revenue
            var entry = new JournalEntryDTO
            {
                Date = DateTime.UtcNow,
                Description = $"Invoice #{invoice.Id}: {invoiceDto.Description}",
                Reference = invoice.Id.ToString(),
                Lines = new List<JournalEntryLineDTO>
                {
                    new JournalEntryLineDTO { AccountId = 1100, Debit = invoiceDto.Amount, Credit = 0 }, // Dr AR
                    new JournalEntryLineDTO { AccountId = invoiceDto.RevenueAccountId, Debit = 0, Credit = invoiceDto.Amount } // Cr Revenue
                }
            };

            await _ledgerService.CreateJournalEntryAsync(entry, "System");
            await _context.SaveChangesAsync();
            return invoice;
        }

        public async Task<Payment> ReceivePaymentAsync(int invoiceId, decimal amount, string paymentMethod, string userId)
        {
            var invoice = await _context.Invoices.FindAsync(invoiceId);
            if (invoice == null) throw new Exception("Invoice not found");

            invoice.PaidAmount += amount;
            if (invoice.PaidAmount >= invoice.TotalAmount) invoice.Status = "Paid";

            var payment = new Payment
            {
                InvoiceId = invoiceId,
                Amount = amount,
                Date = DateTime.UtcNow,
                PaymentMethod = paymentMethod,
                Type = "Incoming"
            };
            _context.Payments.Add(payment);

            // GL: Dr Cash (1000), Cr Accounts Receivable (1100)
            var entry = new JournalEntryDTO
            {
                Date = DateTime.UtcNow,
                Description = $"Payment received for Invoice #{invoice.Id}",
                Reference = $"REC-{payment.Id}",
                Lines = new List<JournalEntryLineDTO>
                {
                    new JournalEntryLineDTO { AccountId = 1000, Debit = amount, Credit = 0 }, // Dr Cash
                    new JournalEntryLineDTO { AccountId = 1100, Debit = 0, Credit = amount }  // Cr AR
                }
            };

            await _ledgerService.CreateJournalEntryAsync(entry, userId);
            await _context.SaveChangesAsync();
            return payment;
        }
    }
}