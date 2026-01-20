using AccountingSystem.API.Data;
using AccountingSystem.API.Services.Interfaces;
using AccountingSystem.Shared.DTOs;
using AccountingSystem.API.Models;
using AccountingSystem.Shared.Enums; 
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

        public async Task<List<VendorDTO>> GetVendorsAsync()
        {
            return await _context.Vendors
                .Select(v => new VendorDTO { Id = v.Id, Name = v.Name, Email = v.Email, ContactPerson = v.ContactPerson })
                .ToListAsync();
        }

        public async Task<Bill> CreateBillAsync(CreateBillDTO billDto)
        {
            var bill = new Bill
            {
                VendorId = billDto.VendorId,
                DueDate = billDto.DueDate,
                Amount = billDto.Amount,
                ReferenceNumber = billDto.ReferenceNumber,
                Description = billDto.Description,
                AmountPaid = 0,
                Status = DocumentStatus.Unpaid // Enum Assignment
            };
            _context.Bills.Add(bill);

            var apAccount = await _context.Accounts.FirstOrDefaultAsync(a => a.Code == "2000");
            if (apAccount == null) throw new Exception("Critical Error: Accounts Payable (2000) account not found.");

            var entry = new JournalEntryDTO
            {
                Date = DateTime.UtcNow,
                Description = $"Bill #{billDto.ReferenceNumber}: {billDto.Description}",
                Reference = billDto.ReferenceNumber,
                Lines = new List<JournalEntryLineDTO>
                {
                    new JournalEntryLineDTO { AccountId = billDto.ExpenseAccountId, Debit = billDto.Amount, Credit = 0 },
                    new JournalEntryLineDTO { AccountId = apAccount.Id, Debit = 0, Credit = billDto.Amount }
                }
            };

            await _ledgerService.CreateJournalEntryAsync(entry, "System");
            await _context.SaveChangesAsync();
            return bill;
        }

        public async Task<Payment> PayBillAsync(RecordPaymentDTO paymentDto, string userId)
        {
            var bill = await _context.Bills.FindAsync(paymentDto.ReferenceId);
            if (bill == null) throw new Exception("Bill not found");

            if (paymentDto.Amount > (bill.Amount - bill.AmountPaid))
                throw new Exception($"Overpayment detected.");

            bill.AmountPaid += paymentDto.Amount;

            // Logic using Enums
            if (bill.AmountPaid >= bill.Amount - 0.01m)
                bill.Status = DocumentStatus.Paid;
            else
                bill.Status = DocumentStatus.PartiallyPaid;

            var payment = new Payment
            {
                BillId = bill.Id,
                Amount = paymentDto.Amount,
                Date = paymentDto.PaymentDate,
                PaymentMethod = paymentDto.PaymentMethod, // Enum
                ReferenceNumber = paymentDto.ReferenceNumber,
                Remarks = paymentDto.Remarks,
                Type = PaymentType.Outgoing, // Enum
                AccountId = paymentDto.AssetAccountId,
                CreatedById = int.TryParse(userId, out int uid) ? uid : null // Audit
            };
            _context.Payments.Add(payment);

            var apAccount = await _context.Accounts.FirstOrDefaultAsync(a => a.Code == "2000");
            var entry = new JournalEntryDTO
            {
                Date = paymentDto.PaymentDate,
                Description = $"Payment for Bill #{bill.ReferenceNumber}",
                Reference = paymentDto.ReferenceNumber ?? $"PAY-{payment.Id}",
                Lines = new List<JournalEntryLineDTO>
                {
                    new JournalEntryLineDTO { AccountId = apAccount.Id, Debit = paymentDto.Amount, Credit = 0 },
                    new JournalEntryLineDTO { AccountId = paymentDto.AssetAccountId, Debit = 0, Credit = paymentDto.Amount }
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
        private readonly IPaymentService _paymentService;

        public ReceivableService(AccountingDbContext context, ILedgerService ledgerService, IPaymentService paymentService)
        {
            _context = context;
            _ledgerService = ledgerService;
            _paymentService = paymentService;
        }

        public async Task<List<CustomerDTO>> GetCustomersAsync()
        {
            return await _context.Customers
                .Select(c => new CustomerDTO { Id = c.Id, Name = c.Name, Email = c.Email, Phone = c.Phone })
                .ToListAsync();
        }

        public async Task<Invoice> CreateInvoiceAsync(CreateInvoiceDTO invoiceDto)
        {
            var invoice = new Invoice
            {
                CustomerId = invoiceDto.CustomerId,
                DueDate = invoiceDto.DueDate,
                TotalAmount = invoiceDto.Amount,
                Description = invoiceDto.Description,
                PaidAmount = 0,
                Status = DocumentStatus.Unpaid // Enum
            };
            _context.Invoices.Add(invoice);

            var arAccount = await _context.Accounts.FirstOrDefaultAsync(a => a.Code == "1100");
            if (arAccount == null) throw new Exception("Critical Error: Accounts Receivable (1100) missing.");

            var entry = new JournalEntryDTO
            {
                Date = DateTime.UtcNow,
                Description = $"Invoice #{invoice.Id}: {invoiceDto.Description}",
                Reference = invoice.Id.ToString(),
                Lines = new List<JournalEntryLineDTO>
                {
                    new JournalEntryLineDTO { AccountId = arAccount.Id, Debit = invoiceDto.Amount, Credit = 0 },
                    new JournalEntryLineDTO { AccountId = invoiceDto.RevenueAccountId, Debit = 0, Credit = invoiceDto.Amount }
                }
            };

            await _ledgerService.CreateJournalEntryAsync(entry, "System");
            await _context.SaveChangesAsync();
            return invoice;
        }

        public async Task<Payment> ReceivePaymentAsync(RecordPaymentDTO paymentDto, string userId)
        {
            var invoice = await _context.Invoices.FindAsync(paymentDto.ReferenceId);
            if (invoice == null) throw new Exception("Invoice not found");

            // Enum Check
            if (paymentDto.PaymentMethod == PaymentMethod.Online && !string.IsNullOrEmpty(paymentDto.SourceId))
            {
                await _paymentService.CapturePaymentAsync(paymentDto.SourceId, paymentDto.Amount, $"Inv #{invoice.Id}");
            }

            if (paymentDto.Amount > (invoice.TotalAmount - invoice.PaidAmount))
                throw new Exception($"Overpayment detected.");

            invoice.PaidAmount += paymentDto.Amount;

            // Logic using Enums
            if (invoice.PaidAmount >= invoice.TotalAmount - 0.01m)
                invoice.Status = DocumentStatus.Paid;
            else
                invoice.Status = DocumentStatus.PartiallyPaid;

            var payment = new Payment
            {
                InvoiceId = invoice.Id,
                Amount = paymentDto.Amount,
                Date = paymentDto.PaymentDate,
                PaymentMethod = paymentDto.PaymentMethod,
                ReferenceNumber = paymentDto.ReferenceNumber,
                Remarks = paymentDto.Remarks,
                Type = PaymentType.Incoming, // Enum
                AccountId = paymentDto.AssetAccountId,
                CreatedById = int.TryParse(userId, out int uid) ? uid : null
            };
            _context.Payments.Add(payment);

            var arAccount = await _context.Accounts.FirstOrDefaultAsync(a => a.Code == "1100");
            var entry = new JournalEntryDTO
            {
                Date = paymentDto.PaymentDate,
                Description = $"Payment received for Invoice #{invoice.Id}",
                Reference = paymentDto.ReferenceNumber ?? $"REC-{payment.Id}",
                Lines = new List<JournalEntryLineDTO>
                {
                    new JournalEntryLineDTO { AccountId = paymentDto.AssetAccountId, Debit = paymentDto.Amount, Credit = 0 },
                    new JournalEntryLineDTO { AccountId = arAccount.Id, Debit = 0, Credit = paymentDto.Amount }
                }
            };

            await _ledgerService.CreateJournalEntryAsync(entry, userId);
            await _context.SaveChangesAsync();
            return payment;
        }
    }
}