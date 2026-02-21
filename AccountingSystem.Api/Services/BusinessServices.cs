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
        private readonly IFiscalYearService _fiscalYearService;

        public PayableService(AccountingDbContext context, ILedgerService ledgerService, IFiscalYearService fiscalYearService)
        {
            _context = context;
            _ledgerService = ledgerService;
            _fiscalYearService = fiscalYearService;
        }

        public async Task<List<VendorDTO>> GetVendorsAsync(bool includeArchived = false)
        {
            var query = _context.Vendors.AsQueryable();

            if (includeArchived)
            {
                query = query.IgnoreQueryFilters();
            }

            return await query
                .Select(v => new VendorDTO
                {
                    Id = v.Id,
                    Name = v.Name,
                    Email = v.Email ?? string.Empty,
                    ContactPerson = v.ContactPerson ?? string.Empty,
                    IsActive = v.IsActive,
                    IsDeleted = v.IsDeleted
                })
                .ToListAsync();
        }

        // --- Vendor CRUD ---
        public async Task<Vendor> CreateVendorAsync(CreateVendorDTO vendorDto)
        {
            var vendor = new Vendor
            {
                Name = vendorDto.Name,
                Email = vendorDto.Email,
                ContactPerson = vendorDto.ContactPerson,
                IsActive = true
            };
            _context.Vendors.Add(vendor);
            await _context.SaveChangesAsync();
            return vendor;
        }

        public async Task<Vendor> UpdateVendorAsync(int id, UpdateVendorDTO vendorDto)
        {
            var vendor = await _context.Vendors.FindAsync(id);
            if (vendor == null) throw new Exception("Vendor not found");

            vendor.Name = vendorDto.Name;
            vendor.Email = vendorDto.Email;
            vendor.ContactPerson = vendorDto.ContactPerson;
            await _context.SaveChangesAsync();
            return vendor;
        }

        public async Task DeleteVendorAsync(int id)
        {
            var vendor = await _context.Vendors.FindAsync(id);
            if (vendor == null) throw new Exception("Vendor not found");

            // Soft Delete
            vendor.IsDeleted = true;
            vendor.IsActive = false;
            await _context.SaveChangesAsync();
        }

        public async Task RestoreVendorAsync(int id)
        {
            var vendor = await _context.Vendors.IgnoreQueryFilters().FirstOrDefaultAsync(v => v.Id == id);
            if (vendor == null) throw new Exception("Vendor not found");

            vendor.IsDeleted = false;
            vendor.IsActive = true;
            await _context.SaveChangesAsync();
        }
        // -------------------

        public async Task<List<BillDTO>> GetBillsAsync()
        {
            return await _context.Bills
                .Include(b => b.Vendor)
                .Select(b => new BillDTO
                {
                    Id = b.Id,
                    VendorId = b.VendorId,
                    VendorName = b.Vendor.Name,
                    DueDate = b.DueDate,
                    Amount = b.Amount,
                    ReferenceNumber = b.ReferenceNumber,
                    Description = b.Description ?? string.Empty,
                    AmountPaid = b.AmountPaid,
                    Status = b.Status
                })
                .OrderByDescending(b => b.DueDate)
                .ToListAsync();
        }

        public async Task<Bill> CreateBillAsync(CreateBillDTO billDto)
        {
            await _fiscalYearService.EnsureDateOpenAsync(billDto.DueDate);
            var bill = new Bill
            {
                VendorId = billDto.VendorId,
                DueDate = billDto.DueDate,
                Amount = billDto.Amount,
                ReferenceNumber = billDto.ReferenceNumber,
                Description = billDto.Description,
                AmountPaid = 0,
                Status = DocumentStatus.Unpaid
            };
            _context.Bills.Add(bill);

            var apAccount = await _context.Accounts.FirstOrDefaultAsync(a => a.Code == "2000");
            if (apAccount == null) throw new Exception("Critical Error: Accounts Payable (2000) account not found.");

            var entry = new JournalEntryDTO
            {
                Date = billDto.DueDate,
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
            await _fiscalYearService.EnsureDateOpenAsync(paymentDto.PaymentDate);
            var bill = await _context.Bills.FindAsync(paymentDto.ReferenceId);
            if (bill == null) throw new Exception("Bill not found");

            if (paymentDto.Amount > (bill.Amount - bill.AmountPaid))
                throw new Exception("Overpayment detected.");

            bill.AmountPaid += paymentDto.Amount;

            if (bill.AmountPaid >= bill.Amount - 0.01m)
                bill.Status = DocumentStatus.Paid;
            else
                bill.Status = DocumentStatus.PartiallyPaid;

            var payment = new Payment
            {
                BillId = bill.Id,
                Amount = paymentDto.Amount,
                Date = paymentDto.PaymentDate,
                PaymentMethod = paymentDto.PaymentMethod,
                ReferenceNumber = paymentDto.ReferenceNumber,
                Remarks = paymentDto.Remarks,
                Type = PaymentType.Outgoing,
                AccountId = paymentDto.AssetAccountId,
                CreatedById = int.TryParse(userId, out int uid) ? uid : null
            };
            _context.Payments.Add(payment);

            var apAccount = await _context.Accounts.FirstOrDefaultAsync(a => a.Code == "2000");
            if (apAccount == null) throw new Exception("Critical Error: Accounts Payable (2000) account not found.");

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
        private readonly IFiscalYearService _fiscalYearService;

        public ReceivableService(AccountingDbContext context, ILedgerService ledgerService, IPaymentService paymentService, IFiscalYearService fiscalYearService)
        {
            _context = context;
            _ledgerService = ledgerService;
            _paymentService = paymentService;
            _fiscalYearService = fiscalYearService;
        }

        public async Task<List<CustomerDTO>> GetCustomersAsync(bool includeArchived = false)
        {
            var query = _context.Customers.AsQueryable();

            if (includeArchived)
            {
                query = query.IgnoreQueryFilters();
            }

            return await query
                .Select(c => new CustomerDTO
                {
                    Id = c.Id,
                    Name = c.Name,
                    Email = c.Email ?? string.Empty,
                    Phone = c.Phone ?? string.Empty, 
                    IsActive = c.IsActive,
                    IsDeleted = c.IsDeleted
                })
                .ToListAsync();
        }

        // --- Customer CRUD ---
        public async Task<Customer> CreateCustomerAsync(CreateCustomerDTO customerDto)
        {
            var customer = new Customer
            {
                Name = customerDto.Name,
                Email = customerDto.Email,
                Phone = customerDto.Phone,
                IsActive = true
            };
            _context.Customers.Add(customer);
            await _context.SaveChangesAsync();
            return customer;
        }

        public async Task<Customer> UpdateCustomerAsync(int id, UpdateCustomerDTO customerDto)
        {
            var customer = await _context.Customers.FindAsync(id);
            if (customer == null) throw new Exception("Customer not found");

            customer.Name = customerDto.Name;
            customer.Email = customerDto.Email;
            customer.Phone = customerDto.Phone;
            await _context.SaveChangesAsync();
            return customer;
        }

        public async Task DeleteCustomerAsync(int id)
        {
            var customer = await _context.Customers.FindAsync(id);
            if (customer == null) throw new Exception("Customer not found");

            // Soft Delete
            customer.IsDeleted = true;
            customer.IsActive = false;
            await _context.SaveChangesAsync();
        }

        public async Task RestoreCustomerAsync(int id)
        {
            var customer = await _context.Customers.IgnoreQueryFilters().FirstOrDefaultAsync(c => c.Id == id);
            if (customer == null) throw new Exception("Customer not found");

            customer.IsDeleted = false;
            customer.IsActive = true;
            await _context.SaveChangesAsync();
        }
        // ---------------------

        public async Task<List<InvoiceDTO>> GetInvoicesAsync()
        {
            return await _context.Invoices
                .Include(i => i.Customer)
                .Select(i => new InvoiceDTO
                {
                    Id = i.Id,
                    CustomerId = i.CustomerId,
                    CustomerName = i.Customer.Name,
                    DueDate = i.DueDate,
                    TotalAmount = i.TotalAmount,
                    Description = i.Description ?? string.Empty,
                    PaidAmount = i.PaidAmount,
                    Status = i.Status
                })
                .OrderByDescending(i => i.DueDate)
                .ToListAsync();
        }

        public async Task<Invoice> CreateInvoiceAsync(CreateInvoiceDTO invoiceDto)
        {
            await _fiscalYearService.EnsureDateOpenAsync(invoiceDto.DueDate);
            var invoice = new Invoice
            {
                CustomerId = invoiceDto.CustomerId,
                DueDate = invoiceDto.DueDate,
                TotalAmount = invoiceDto.Amount,
                Description = invoiceDto.Description,
                PaidAmount = 0,
                Status = DocumentStatus.Unpaid
            };
            _context.Invoices.Add(invoice);

            var arAccount = await _context.Accounts.FirstOrDefaultAsync(a => a.Code == "1100");
            if (arAccount == null) throw new Exception("Critical Error: Accounts Receivable (1100) missing.");

            var entry = new JournalEntryDTO
            {
                Date = billDto.DueDate,
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
            await _fiscalYearService.EnsureDateOpenAsync(paymentDto.PaymentDate);
            var invoice = await _context.Invoices.FindAsync(paymentDto.ReferenceId);
            if (invoice == null) throw new Exception("Invoice not found");

            // PayMongo Capture Logic
            if (paymentDto.PaymentMethod == PaymentMethod.Online && !string.IsNullOrEmpty(paymentDto.SourceId))
            {
                await _paymentService.CapturePaymentAsync(paymentDto.SourceId, paymentDto.Amount, $"Inv #{invoice.Id}");
            }

            if (paymentDto.Amount > (invoice.TotalAmount - invoice.PaidAmount))
                throw new Exception("Overpayment detected.");

            invoice.PaidAmount += paymentDto.Amount;

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
                Type = PaymentType.Incoming,
                AccountId = paymentDto.AssetAccountId,
                CreatedById = int.TryParse(userId, out int uid) ? uid : null
            };
            _context.Payments.Add(payment);

            var arAccount = await _context.Accounts.FirstOrDefaultAsync(a => a.Code == "1100");
            if (arAccount == null) throw new Exception("Critical Error: Accounts Receivable (1100) missing.");

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