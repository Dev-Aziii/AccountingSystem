using AccountingSystem.API.Data;
using AccountingSystem.API.Models;
using AccountingSystem.API.Services.Interfaces;
using AccountingSystem.Shared.DTOs;
using AccountingSystem.Shared.Enums;
using Microsoft.EntityFrameworkCore;

namespace AccountingSystem.API.Services
{
    public class PayableService : IPayableService
    {
        private readonly AccountingDbContext _context;
        private readonly ILedgerService _ledgerService;
        private readonly IYearEndCloseService _yearEndCloseService;
        private readonly IDocumentSequenceService _documentSequenceService;
        private readonly ITenantService _tenantService;

        public PayableService(
            AccountingDbContext context,
            ILedgerService ledgerService,
            IYearEndCloseService yearEndCloseService,
            IDocumentSequenceService documentSequenceService,
            ITenantService tenantService)
        {
            _context = context;
            _ledgerService = ledgerService;
            _yearEndCloseService = yearEndCloseService;
            _documentSequenceService = documentSequenceService;
            _tenantService = tenantService;
        }

        public async Task<List<VendorDTO>> GetVendorsAsync(bool includeArchived = false)
        {
            var query = _context.Vendors.AsQueryable();
            if (includeArchived) query = query.IgnoreQueryFilters();

            return await query.Select(v => new VendorDTO
            {
                Id = v.Id,
                Name = v.Name,
                Email = v.Email ?? string.Empty,
                ContactPerson = v.ContactPerson ?? string.Empty,
                IsActive = v.IsActive,
                IsDeleted = v.IsDeleted
            }).ToListAsync();
        }

        public async Task<Vendor> CreateVendorAsync(CreateVendorDTO vendorDto)
        {
            var vendor = new Vendor { Name = vendorDto.Name, Email = vendorDto.Email, ContactPerson = vendorDto.ContactPerson, IsActive = true };
            _context.Vendors.Add(vendor);
            await _context.SaveChangesAsync();
            return vendor;
        }

        public async Task<Vendor> UpdateVendorAsync(int id, UpdateVendorDTO vendorDto)
        {
            var vendor = await _context.Vendors.FindAsync(id) ?? throw new Exception("Vendor not found");
            vendor.Name = vendorDto.Name;
            vendor.Email = vendorDto.Email;
            vendor.ContactPerson = vendorDto.ContactPerson;
            await _context.SaveChangesAsync();
            return vendor;
        }

        public async Task DeleteVendorAsync(int id)
        {
            var vendor = await _context.Vendors.FindAsync(id) ?? throw new Exception("Vendor not found");
            vendor.IsDeleted = true;
            vendor.IsActive = false;
            await _context.SaveChangesAsync();
        }

        public async Task RestoreVendorAsync(int id)
        {
            var vendor = await _context.Vendors.IgnoreQueryFilters().FirstOrDefaultAsync(v => v.Id == id) ?? throw new Exception("Vendor not found");
            vendor.IsDeleted = false;
            vendor.IsActive = true;
            await _context.SaveChangesAsync();
        }

        public async Task<List<BillDTO>> GetBillsAsync(int? fiscalYear = null, DocumentStatus? status = null)
        {
            var query = _context.Bills.Include(b => b.Vendor).AsQueryable();
            if (fiscalYear.HasValue)
            {
                var period = _yearEndCloseService.ResolveFiscalPeriod(fiscalYear.Value);
                query = query.Where(b => b.DueDate >= period.StartDate && b.DueDate < period.EndDate.Date.AddDays(1));
            }
            if (status.HasValue) query = query.Where(b => b.Status == status.Value);

            return await query.Select(b => new BillDTO
            {
                Id = b.Id,
                VendorId = b.VendorId,
                VendorName = b.Vendor.Name,
                DueDate = b.DueDate,
                Amount = b.Amount,
                VendorReferenceNumber = b.VendorReferenceNumber,
                SystemReferenceNumber = b.SystemReferenceNumber,
                Description = b.Description ?? string.Empty,
                AmountPaid = b.AmountPaid,
                Status = b.Status
            }).OrderByDescending(b => b.DueDate).ToListAsync();
        }

        public async Task<Bill> CreateBillAsync(CreateBillDTO billDto)
        {
            await using var tx = await _context.Database.BeginTransactionAsync();

            var bill = new Bill
            {
                VendorId = billDto.VendorId,
                DueDate = billDto.DueDate,
                Amount = billDto.Amount,
                VendorReferenceNumber = billDto.VendorReferenceNumber,
                SystemReferenceNumber = await _documentSequenceService.GetNextSequenceAsync(_tenantService.GetCurrentTenant(), DocumentType.CheckPayment),
                Description = billDto.Description,
                AmountPaid = 0,
                Status = DocumentStatus.Unpaid
            };
            _context.Bills.Add(bill);

            var apAccount = await _context.Accounts.FirstOrDefaultAsync(a => a.Code == "2000") ?? throw new Exception("Critical Error: Accounts Payable (2000) account not found.");
            var entry = new JournalEntryDTO
            {
                Date = DateTime.UtcNow,
                Description = $"Bill {bill.SystemReferenceNumber} ({bill.VendorReferenceNumber}): {billDto.Description}",
                Lines = new List<JournalEntryLineDTO>
                {
                    new() { AccountId = billDto.ExpenseAccountId, Debit = billDto.Amount, Credit = 0 },
                    new() { AccountId = apAccount.Id, Debit = 0, Credit = billDto.Amount }
                }
            };

            await _ledgerService.CreateJournalEntryAsync(entry, "System", saveImmediately: false);
            await _context.SaveChangesAsync();
            await tx.CommitAsync();
            return bill;
        }

        public async Task<PaymentCreationResultDTO> PayBillAsync(RecordPaymentDTO paymentDto, string userId)
        {
            await using var tx = await _context.Database.BeginTransactionAsync();

            var bill = await _context.Bills.FindAsync(paymentDto.ReferenceId) ?? throw new Exception("Bill not found");
            if (paymentDto.Amount > (bill.Amount - bill.AmountPaid)) throw new Exception("Overpayment detected.");

            bill.AmountPaid += paymentDto.Amount;
            bill.Status = bill.AmountPaid >= bill.Amount - 0.01m ? DocumentStatus.Paid : DocumentStatus.PartiallyPaid;

            var payment = new Payment
            {
                BillId = bill.Id,
                Amount = paymentDto.Amount,
                Date = paymentDto.PaymentDate,
                PaymentMethod = paymentDto.PaymentMethod,
                ReferenceNumber = await _documentSequenceService.GetNextSequenceAsync(_tenantService.GetCurrentTenant(), DocumentType.CheckPayment),
                Remarks = paymentDto.Remarks,
                Type = PaymentType.Outgoing,
                AccountId = paymentDto.AssetAccountId,
                CreatedById = int.TryParse(userId, out var uid) ? uid : null
            };
            _context.Payments.Add(payment);

            var apAccount = await _context.Accounts.FirstOrDefaultAsync(a => a.Code == "2000") ?? throw new Exception("Critical Error: Accounts Payable (2000) account not found.");
            var entry = new JournalEntryDTO
            {
                Date = paymentDto.PaymentDate,
                Description = $"Payment for Bill {bill.SystemReferenceNumber}",
                Lines = new List<JournalEntryLineDTO>
                {
                    new() { AccountId = apAccount.Id, Debit = paymentDto.Amount, Credit = 0 },
                    new() { AccountId = paymentDto.AssetAccountId, Debit = 0, Credit = paymentDto.Amount }
                }
            };

            await _ledgerService.CreateJournalEntryAsync(entry, userId, saveImmediately: false);
            await _context.SaveChangesAsync();
            await tx.CommitAsync();

            return new PaymentCreationResultDTO { Id = payment.Id, ReferenceNumber = payment.ReferenceNumber ?? string.Empty };
        }
    }

    public class ReceivableService : IReceivableService
    {
        private readonly AccountingDbContext _context;
        private readonly ILedgerService _ledgerService;
        private readonly IPaymentService _paymentService;
        private readonly IYearEndCloseService _yearEndCloseService;
        private readonly IDocumentSequenceService _documentSequenceService;
        private readonly ITenantService _tenantService;

        public ReceivableService(
            AccountingDbContext context,
            ILedgerService ledgerService,
            IPaymentService paymentService,
            IYearEndCloseService yearEndCloseService,
            IDocumentSequenceService documentSequenceService,
            ITenantService tenantService)
        {
            _context = context;
            _ledgerService = ledgerService;
            _paymentService = paymentService;
            _yearEndCloseService = yearEndCloseService;
            _documentSequenceService = documentSequenceService;
            _tenantService = tenantService;
        }

        public async Task<List<CustomerDTO>> GetCustomersAsync(bool includeArchived = false)
        {
            var query = _context.Customers.AsQueryable();
            if (includeArchived) query = query.IgnoreQueryFilters();

            return await query.Select(c => new CustomerDTO
            {
                Id = c.Id,
                Name = c.Name,
                Email = c.Email ?? string.Empty,
                Phone = c.Phone ?? string.Empty,
                IsActive = c.IsActive,
                IsDeleted = c.IsDeleted
            }).ToListAsync();
        }

        public async Task<Customer> CreateCustomerAsync(CreateCustomerDTO customerDto)
        {
            var customer = new Customer { Name = customerDto.Name, Email = customerDto.Email, Phone = customerDto.Phone, IsActive = true };
            _context.Customers.Add(customer);
            await _context.SaveChangesAsync();
            return customer;
        }

        public async Task<Customer> UpdateCustomerAsync(int id, UpdateCustomerDTO customerDto)
        {
            var customer = await _context.Customers.FindAsync(id) ?? throw new Exception("Customer not found");
            customer.Name = customerDto.Name;
            customer.Email = customerDto.Email;
            customer.Phone = customerDto.Phone;
            await _context.SaveChangesAsync();
            return customer;
        }

        public async Task DeleteCustomerAsync(int id)
        {
            var customer = await _context.Customers.FindAsync(id) ?? throw new Exception("Customer not found");
            customer.IsDeleted = true;
            customer.IsActive = false;
            await _context.SaveChangesAsync();
        }

        public async Task RestoreCustomerAsync(int id)
        {
            var customer = await _context.Customers.IgnoreQueryFilters().FirstOrDefaultAsync(c => c.Id == id) ?? throw new Exception("Customer not found");
            customer.IsDeleted = false;
            customer.IsActive = true;
            await _context.SaveChangesAsync();
        }

        public async Task<List<InvoiceDTO>> GetInvoicesAsync(int? fiscalYear = null, DocumentStatus? status = null)
        {
            var query = _context.Invoices.Include(i => i.Customer).AsQueryable();
            if (fiscalYear.HasValue)
            {
                var period = _yearEndCloseService.ResolveFiscalPeriod(fiscalYear.Value);
                query = query.Where(i => i.DueDate >= period.StartDate && i.DueDate < period.EndDate.Date.AddDays(1));
            }
            if (status.HasValue) query = query.Where(i => i.Status == status.Value);

            return await query.Select(i => new InvoiceDTO
            {
                Id = i.Id,
                CustomerId = i.CustomerId,
                CustomerName = i.Customer.Name,
                DueDate = i.DueDate,
                TotalAmount = i.TotalAmount,
                ReferenceNumber = i.ReferenceNumber,
                Description = i.Description ?? string.Empty,
                PaidAmount = i.PaidAmount,
                Status = i.Status
            }).OrderByDescending(i => i.DueDate).ToListAsync();
        }

        public async Task<DocumentCreationResultDTO> CreateInvoiceAsync(CreateInvoiceDTO invoiceDto)
        {
            await using var tx = await _context.Database.BeginTransactionAsync();

            var invoice = new Invoice
            {
                CustomerId = invoiceDto.CustomerId,
                DueDate = invoiceDto.DueDate,
                TotalAmount = invoiceDto.Amount,
                Description = invoiceDto.Description,
                ReferenceNumber = await _documentSequenceService.GetNextSequenceAsync(_tenantService.GetCurrentTenant(), DocumentType.Invoice),
                PaidAmount = 0,
                Status = DocumentStatus.Unpaid
            };
            _context.Invoices.Add(invoice);

            var arAccount = await _context.Accounts.FirstOrDefaultAsync(a => a.Code == "1100") ?? throw new Exception("Critical Error: Accounts Receivable (1100) missing.");
            var entry = new JournalEntryDTO
            {
                Date = DateTime.UtcNow,
                Description = $"Invoice {invoice.ReferenceNumber}: {invoiceDto.Description}",
                Lines = new List<JournalEntryLineDTO>
                {
                    new() { AccountId = arAccount.Id, Debit = invoiceDto.Amount, Credit = 0 },
                    new() { AccountId = invoiceDto.RevenueAccountId, Debit = 0, Credit = invoiceDto.Amount }
                }
            };

            await _ledgerService.CreateJournalEntryAsync(entry, "System", saveImmediately: false);
            await _context.SaveChangesAsync();
            await tx.CommitAsync();
            return new DocumentCreationResultDTO { Id = invoice.Id, ReferenceNumber = invoice.ReferenceNumber };
        }

        public async Task<PaymentCreationResultDTO> ReceivePaymentAsync(RecordPaymentDTO paymentDto, string userId)
        {
            await using var tx = await _context.Database.BeginTransactionAsync();

            var invoice = await _context.Invoices.FindAsync(paymentDto.ReferenceId) ?? throw new Exception("Invoice not found");
            if (paymentDto.PaymentMethod == PaymentMethod.Online && !string.IsNullOrEmpty(paymentDto.SourceId))
            {
                await _paymentService.CapturePaymentAsync(paymentDto.SourceId, paymentDto.Amount, $"Inv #{invoice.ReferenceNumber}");
            }
            if (paymentDto.Amount > (invoice.TotalAmount - invoice.PaidAmount)) throw new Exception("Overpayment detected.");

            invoice.PaidAmount += paymentDto.Amount;
            invoice.Status = invoice.PaidAmount >= invoice.TotalAmount - 0.01m ? DocumentStatus.Paid : DocumentStatus.PartiallyPaid;

            var payment = new Payment
            {
                InvoiceId = invoice.Id,
                Amount = paymentDto.Amount,
                Date = paymentDto.PaymentDate,
                PaymentMethod = paymentDto.PaymentMethod,
                ReferenceNumber = await _documentSequenceService.GetNextSequenceAsync(_tenantService.GetCurrentTenant(), DocumentType.PaymentReceived),
                Remarks = paymentDto.Remarks,
                Type = PaymentType.Incoming,
                AccountId = paymentDto.AssetAccountId,
                CreatedById = int.TryParse(userId, out var uid) ? uid : null
            };
            _context.Payments.Add(payment);

            var arAccount = await _context.Accounts.FirstOrDefaultAsync(a => a.Code == "1100") ?? throw new Exception("Critical Error: Accounts Receivable (1100) missing.");
            var entry = new JournalEntryDTO
            {
                Date = paymentDto.PaymentDate,
                Description = $"Payment received for Invoice {invoice.ReferenceNumber}",
                Lines = new List<JournalEntryLineDTO>
                {
                    new() { AccountId = paymentDto.AssetAccountId, Debit = paymentDto.Amount, Credit = 0 },
                    new() { AccountId = arAccount.Id, Debit = 0, Credit = paymentDto.Amount }
                }
            };

            await _ledgerService.CreateJournalEntryAsync(entry, userId, saveImmediately: false);
            await _context.SaveChangesAsync();
            await tx.CommitAsync();

            return new PaymentCreationResultDTO { Id = payment.Id, ReferenceNumber = payment.ReferenceNumber ?? string.Empty };
        }
    }
}
