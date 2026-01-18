using AccountingSystem.API.Data;
using AccountingSystem.API.Models;
using AccountingSystem.API.Services.Interfaces;
using AccountingSystem.Shared.DTOs;
using Microsoft.EntityFrameworkCore;

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

            // 2. Fetch the correct AP Account ID (Code: "2000")
            var apAccount = await _context.Accounts.FirstOrDefaultAsync(a => a.Code == "2000");
            if (apAccount == null) throw new Exception("Critical Error: Accounts Payable (2000) account not found in Ledger.");

            // 3. Post to GL: Dr Expense, Cr Accounts Payable
            var entry = new JournalEntryDTO
            {
                Date = DateTime.UtcNow,
                Description = $"Bill #{billDto.ReferenceNumber}: {billDto.Description}",
                Reference = billDto.ReferenceNumber,
                Lines = new List<JournalEntryLineDTO>
                {
                    new JournalEntryLineDTO { AccountId = billDto.ExpenseAccountId, Debit = billDto.Amount, Credit = 0 },
                    new JournalEntryLineDTO { AccountId = apAccount.Id, Debit = 0, Credit = billDto.Amount } // Use fetched ID
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

            // 2. Fetch Accounts (AP: 2000, Cash: 1000)
            var apAccount = await _context.Accounts.FirstOrDefaultAsync(a => a.Code == "2000");
            var cashAccount = await _context.Accounts.FirstOrDefaultAsync(a => a.Code == "1000");

            if (apAccount == null || cashAccount == null)
                throw new Exception("Critical Error: Default AP (2000) or Cash (1000) accounts missing.");

            // 3. Post to GL: Dr Accounts Payable, Cr Cash/Bank
            var entry = new JournalEntryDTO
            {
                Date = DateTime.UtcNow,
                Description = $"Payment for Bill #{bill.ReferenceNumber}",
                Reference = $"PAY-{payment.Id}",
                Lines = new List<JournalEntryLineDTO>
                {
                    new JournalEntryLineDTO { AccountId = apAccount.Id, Debit = amount, Credit = 0 },
                    new JournalEntryLineDTO { AccountId = cashAccount.Id, Debit = 0, Credit = amount }
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

            // Fetch AR Account (Code: "1100")
            var arAccount = await _context.Accounts.FirstOrDefaultAsync(a => a.Code == "1100");
            if (arAccount == null) throw new Exception("Critical Error: Accounts Receivable (1100) account not found.");

            // GL: Dr Accounts Receivable, Cr Revenue
            var entry = new JournalEntryDTO
            {
                Date = DateTime.UtcNow,
                Description = $"Invoice #{invoice.Id}: {invoiceDto.Description}",
                Reference = invoice.Id.ToString(),
                Lines = new List<JournalEntryLineDTO>
                {
                    new JournalEntryLineDTO { AccountId = arAccount.Id, Debit = invoiceDto.Amount, Credit = 0 }, // Use fetched ID
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

            // Fetch Accounts (Cash: 1000, AR: 1100)
            var cashAccount = await _context.Accounts.FirstOrDefaultAsync(a => a.Code == "1000");
            var arAccount = await _context.Accounts.FirstOrDefaultAsync(a => a.Code == "1100");

            if (arAccount == null || cashAccount == null)
                throw new Exception("Critical Error: Default AR (1100) or Cash (1000) accounts missing.");

            // GL: Dr Cash, Cr Accounts Receivable
            var entry = new JournalEntryDTO
            {
                Date = DateTime.UtcNow,
                Description = $"Payment received for Invoice #{invoice.Id}",
                Reference = $"REC-{payment.Id}",
                Lines = new List<JournalEntryLineDTO>
                {
                    new JournalEntryLineDTO { AccountId = cashAccount.Id, Debit = amount, Credit = 0 },
                    new JournalEntryLineDTO { AccountId = arAccount.Id, Debit = 0, Credit = amount }
                }
            };

            await _ledgerService.CreateJournalEntryAsync(entry, userId);
            await _context.SaveChangesAsync();
            return payment;
        }
    }
}