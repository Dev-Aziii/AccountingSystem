using AccountingSystem.API.Models;
using AccountingSystem.Shared.Enums;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace AccountingSystem.API.Data
{
    public static class DataSeeder
    {
        public static async Task SeedDataAsync(AccountingDbContext context)
        {
            // --- 1. SEED SUPER ADMIN (The SaaS Owner) ---
            var superEmail = "sysadmin@accsys.com";
            if (!await context.Users.IgnoreQueryFilters().AnyAsync(u => u.Email == superEmail))
            {
                var hostCompany = new Company
                {
                    Name = "SaaS Operations",
                    Address = "HQ",
                    TaxId = "000",
                    Currency = "PHP",
                    CreatedAt = DateTime.UtcNow,
                    IsActive = true,
                    FiscalYearStartMonth = 1
                };
                context.Companies.Add(hostCompany);
                await context.SaveChangesAsync();

                CreatePasswordHash("master123", out byte[] h, out byte[] s);
                var superUser = new User
                {
                    CompanyId = hostCompany.Id,
                    Email = superEmail,
                    FullName = "System Owner",
                    RoleId = 4,
                    PasswordHash = Convert.ToBase64String(h),
                    PasswordSalt = Convert.ToBase64String(s),
                    IsActive = true
                };
                context.Users.Add(superUser);
                await context.SaveChangesAsync();
            }

            // --- 2. SEED DEFAULT TENANT (Demo Company) ---
            if (await context.Companies.IgnoreQueryFilters().CountAsync() < 2)
            {
                var company = new Company
                {
                    Name = "Jipos Hardware & Services",
                    Address = "123 Innovation Drive, Tech City",
                    TaxId = "TIN-001-002-003",
                    Currency = "PHP",
                    CreatedAt = DateTime.UtcNow,
                    IsActive = true,
                    FiscalYearStartMonth = 1
                };
                context.Companies.Add(company);
                await context.SaveChangesAsync();

                CreatePasswordHash("admin123", out byte[] adminHash, out byte[] adminSalt);
                CreatePasswordHash("user123", out byte[] userHash, out byte[] userSalt);

                var users = new List<User>
                {
                    new()
                    {
                        CompanyId = company.Id,
                        Email = "admin@accsys.com",
                        FullName = "System Administrator",
                        RoleId = 1,
                        PasswordHash = Convert.ToBase64String(adminHash),
                        PasswordSalt = Convert.ToBase64String(adminSalt),
                        IsActive = true
                    },
                    new()
                    {
                        CompanyId = company.Id,
                        Email = "accountant@accsys.com",
                        FullName = "Maria Santos",
                        RoleId = 2,
                        PasswordHash = Convert.ToBase64String(userHash),
                        PasswordSalt = Convert.ToBase64String(userSalt),
                        IsActive = true
                    },
                    new()
                    {
                        CompanyId = company.Id,
                        Email = "manager@accsys.com",
                        FullName = "John Manager",
                        RoleId = 3,
                        PasswordHash = Convert.ToBase64String(userHash),
                        PasswordSalt = Convert.ToBase64String(userSalt),
                        IsActive = true
                    }
                };
                context.Users.AddRange(users);

                var accounts = new List<Account>
                {
                    new() { CompanyId = company.Id, Code = "1000", Name = "Cash on Hand", Type = "Asset" },
                    new() { CompanyId = company.Id, Code = "1010", Name = "BDO Savings", Type = "Asset" },
                    new() { CompanyId = company.Id, Code = "1100", Name = "Accounts Receivable", Type = "Asset" },
                    new() { CompanyId = company.Id, Code = "1200", Name = "Office Equipment", Type = "Asset" },
                    new() { CompanyId = company.Id, Code = "2000", Name = "Accounts Payable", Type = "Liability" },
                    new() { CompanyId = company.Id, Code = "2010", Name = "VAT Payable", Type = "Liability" },
                    new() { CompanyId = company.Id, Code = "3000", Name = "Owner's Capital", Type = "Equity" },
                    new() { CompanyId = company.Id, Code = "3100", Name = "Retained Earnings", Type = "Equity" },
                    new() { CompanyId = company.Id, Code = "4000", Name = "Service Revenue", Type = "Revenue" },
                    new() { CompanyId = company.Id, Code = "4100", Name = "Sales Revenue", Type = "Revenue" },
                    new() { CompanyId = company.Id, Code = "5000", Name = "Rent Expense", Type = "Expense" },
                    new() { CompanyId = company.Id, Code = "5010", Name = "Utilities Expense", Type = "Expense" },
                    new() { CompanyId = company.Id, Code = "5020", Name = "Salaries Expense", Type = "Expense" },
                    new() { CompanyId = company.Id, Code = "5030", Name = "Office Supplies", Type = "Expense" },
                    new() { CompanyId = company.Id, Code = "5040", Name = "Internet & Comm", Type = "Expense" }
                };
                context.Accounts.AddRange(accounts);

                await context.SaveChangesAsync();
            }

            var demoCompany = await context.Companies
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(c => c.Name == "Jipos Hardware & Services");
            if (demoCompany != null)
            {
                await SeedDemoHistoricalDataAsync(context, demoCompany);
            }
        }

        private static async Task SeedDemoHistoricalDataAsync(AccountingDbContext context, Company company)
        {
            var hasAnyLedgerData = await context.JournalEntries
                .IgnoreQueryFilters()
                .AnyAsync(j => j.CompanyId == company.Id);
            if (hasAnyLedgerData)
                return;

            if (!await context.Vendors.IgnoreQueryFilters().AnyAsync(v => v.CompanyId == company.Id))
            {
                var vendors = new List<Vendor>
                {
                    new() { CompanyId = company.Id, Name = "Metro Office Supplies", Email = "billing@metrooffice.ph", ContactPerson = "Clara Reyes", Phone = "09170000001" },
                    new() { CompanyId = company.Id, Name = "Luzon Utilities Corp", Email = "support@luzonutility.ph", ContactPerson = "Mark Tolentino", Phone = "09170000002" },
                    new() { CompanyId = company.Id, Name = "Northwind Internet Services", Email = "acct@northwindnet.ph", ContactPerson = "Pia Santos", Phone = "09170000003" },
                    new() { CompanyId = company.Id, Name = "Prime Hardware Distribution", Email = "sales@primehardware.ph", ContactPerson = "Juan Dela Cruz", Phone = "09170000004" },
                    new() { CompanyId = company.Id, Name = "Vertex Payroll Services", Email = "finance@vertexpayroll.ph", ContactPerson = "Liza Mendoza", Phone = "09170000005" }
                };
                context.Vendors.AddRange(vendors);
            }

            if (!await context.Customers.IgnoreQueryFilters().AnyAsync(c => c.CompanyId == company.Id))
            {
                var customers = new List<Customer>
                {
                    new() { CompanyId = company.Id, Name = "Apex Construction Inc.", Email = "ap@apexcon.ph", Phone = "09280000001" },
                    new() { CompanyId = company.Id, Name = "Bluefield Trading", Email = "acct@bluefield.ph", Phone = "09280000002" },
                    new() { CompanyId = company.Id, Name = "Crestline Logistics", Email = "finance@crestline.ph", Phone = "09280000003" },
                    new() { CompanyId = company.Id, Name = "DeltaWorks Manufacturing", Email = "payables@deltaworks.ph", Phone = "09280000004" },
                    new() { CompanyId = company.Id, Name = "Evergreen Retail Group", Email = "accounting@evergreen.ph", Phone = "09280000005" }
                };
                context.Customers.AddRange(customers);
            }

            await context.SaveChangesAsync();

            var customersForCompany = await context.Customers
                .IgnoreQueryFilters()
                .Where(c => c.CompanyId == company.Id)
                .OrderBy(c => c.Id)
                .ToListAsync();
            var vendorsForCompany = await context.Vendors
                .IgnoreQueryFilters()
                .Where(v => v.CompanyId == company.Id)
                .OrderBy(v => v.Id)
                .ToListAsync();

            var accounts = await context.Accounts
                .IgnoreQueryFilters()
                .Where(a => a.CompanyId == company.Id)
                .ToDictionaryAsync(a => a.Code, a => a);

            var cashAccount = accounts["1010"];
            var arAccount = accounts["1100"];
            var apAccount = accounts["2000"];
            var revenueAccounts = new[] { accounts["4000"], accounts["4100"] };
            var expenseAccounts = new[] { accounts["5000"], accounts["5010"], accounts["5020"], accounts["5030"], accounts["5040"] };

            var rng = new Random(company.Id * 101 + 73);
            var completedYear = DateTime.UtcNow.Year - 1;
            var startYear = completedYear - 2;

            int invoiceSequence = 1;
            int billSequence = 1;
            int paymentSequence = 1;

            for (int year = startYear; year <= completedYear; year++)
            {
                for (int month = 1; month <= 12; month++)
                {
                    var invoiceIssueDate = new DateTime(year, month, Math.Min(5 + rng.Next(0, 10), 28));
                    var invoiceDueDate = invoiceIssueDate.AddDays(15 + rng.Next(0, 20));
                    var invoiceAmount = GetWholeAmountUpTo50k(rng);
                    var invoicePaidAmount = invoiceAmount;
                    var selectedCustomer = customersForCompany[rng.Next(customersForCompany.Count)];
                    var selectedRevenue = revenueAccounts[rng.Next(revenueAccounts.Length)];

                    var invoice = new Invoice
                    {
                        CompanyId = company.Id,
                        CustomerId = selectedCustomer.Id,
                        DueDate = invoiceDueDate,
                        TotalAmount = invoiceAmount,
                        PaidAmount = invoicePaidAmount,
                        ReferenceNumber = $"INV-{invoiceSequence:D4}",
                        Description = $"Seeded sales transaction for {selectedCustomer.Name} ({year}-{month:D2})",
                        Status = DocumentStatus.Paid
                    };
                    context.Invoices.Add(invoice);
                    await context.SaveChangesAsync();

                    await CreateJournalEntryAsync(
                        context,
                        company.Id,
                        invoiceIssueDate,
                        $"Seed Invoice INV-{year}{month:D2}-{invoiceSequence:D3}",
                        $"SEED-INV-{year}{month:D2}-{invoiceSequence:D3}",
                        (arAccount.Id, invoiceAmount, 0),
                        (selectedRevenue.Id, 0, invoiceAmount));

                    var invoicePaymentDate = invoiceDueDate.AddDays(rng.Next(0, 20));
                    var incomingPayment = new Payment
                    {
                        CompanyId = company.Id,
                        InvoiceId = invoice.Id,
                        AccountId = cashAccount.Id,
                        Date = invoicePaymentDate,
                        Amount = invoicePaidAmount,
                        PaymentMethod = GetPaymentMethod(rng),
                        Type = PaymentType.Incoming,
                        ReferenceNumber = $"PR-{paymentSequence:D4}",
                        Remarks = $"Seed receipt for invoice {invoice.Id}"
                    };

                    context.Payments.Add(incomingPayment);
                    await context.SaveChangesAsync();

                    await CreateJournalEntryAsync(
                        context,
                        company.Id,
                        invoicePaymentDate,
                        $"Seed Payment for Invoice INV-{year}{month:D2}-{invoiceSequence:D3}",
                        $"SEED-REC-{paymentSequence:D4}",
                        (cashAccount.Id, invoicePaidAmount, 0),
                        (arAccount.Id, 0, invoicePaidAmount));
                    paymentSequence++;

                    invoiceSequence++;

                    var billIssueDate = new DateTime(year, month, Math.Min(8 + rng.Next(0, 10), 28));
                    var billDueDate = billIssueDate.AddDays(20 + rng.Next(0, 25));
                    var billAmount = GetWholeAmountUpTo50k(rng);
                    var billPaidAmount = billAmount;
                    var selectedVendor = vendorsForCompany[rng.Next(vendorsForCompany.Count)];
                    var selectedExpense = expenseAccounts[rng.Next(expenseAccounts.Length)];

                    var bill = new Bill
                    {
                        CompanyId = company.Id,
                        VendorId = selectedVendor.Id,
                        DueDate = billDueDate,
                        Amount = billAmount,
                        AmountPaid = billPaidAmount,
                        VendorReferenceNumber = $"SEED-BILL-{year}{month:D2}-{billSequence:D3}",
                        SystemReferenceNumber = $"CHK-{billSequence:D4}",
                        Description = $"Seeded expense transaction from {selectedVendor.Name} ({year}-{month:D2})",
                        Status = DocumentStatus.Paid
                    };
                    context.Bills.Add(bill);
                    await context.SaveChangesAsync();

                    await CreateJournalEntryAsync(
                        context,
                        company.Id,
                        billIssueDate,
                        $"Seed Bill BILL-{year}{month:D2}-{billSequence:D3}",
                        $"SEED-BILL-{year}{month:D2}-{billSequence:D3}",
                        (selectedExpense.Id, billAmount, 0),
                        (apAccount.Id, 0, billAmount));

                    var billPaymentDate = billDueDate.AddDays(rng.Next(0, 20));
                    var outgoingPayment = new Payment
                    {
                        CompanyId = company.Id,
                        BillId = bill.Id,
                        AccountId = cashAccount.Id,
                        Date = billPaymentDate,
                        Amount = billPaidAmount,
                        PaymentMethod = GetPaymentMethod(rng),
                        Type = PaymentType.Outgoing,
                        ReferenceNumber = $"SEED-PAY-{paymentSequence:D4}",
                        Remarks = $"Seed disbursement for bill {bill.SystemReferenceNumber}"
                    };

                    context.Payments.Add(outgoingPayment);
                    await context.SaveChangesAsync();

                    await CreateJournalEntryAsync(
                        context,
                        company.Id,
                        billPaymentDate,
                        $"Seed Payment for Bill BILL-{year}{month:D2}-{billSequence:D3}",
                        $"SEED-PAY-{paymentSequence:D4}",
                        (apAccount.Id, billPaidAmount, 0),
                        (cashAccount.Id, 0, billPaidAmount));
                    paymentSequence++;

                    billSequence++;
                }
            }
        }

        private static async Task CreateJournalEntryAsync(
            AccountingDbContext context,
            int companyId,
            DateTime date,
            string description,
            string reference,
            params (int AccountId, decimal Debit, decimal Credit)[] lines)
        {
            var entry = new JournalEntry
            {
                CompanyId = companyId,
                Date = date,
                Description = description,
                Reference = reference,
                CreatedBy = "Seeder",
                IsPosted = true,
                Lines = lines.Select(l => new JournalEntryLine
                {
                    AccountId = l.AccountId,
                    Debit = l.Debit,
                    Credit = l.Credit
                }).ToList()
            };

            context.JournalEntries.Add(entry);
            await context.SaveChangesAsync();
        }


        private static async Task SeedDocumentSequencesAsync(AccountingDbContext context, int companyId)
        {
            var defaults = new[]
            {
                new DocumentSequence { CompanyId = companyId, DocumentType = DocumentType.Invoice, Prefix = "INV-", NextNumber = 1 },
                new DocumentSequence { CompanyId = companyId, DocumentType = DocumentType.JournalEntry, Prefix = "JE-", NextNumber = 1 },
                new DocumentSequence { CompanyId = companyId, DocumentType = DocumentType.PaymentReceived, Prefix = "PR-", NextNumber = 1 },
                new DocumentSequence { CompanyId = companyId, DocumentType = DocumentType.CheckPayment, Prefix = "CHK-", NextNumber = 1 }
            };

            foreach (var item in defaults)
            {
                var exists = await context.DocumentSequences.IgnoreQueryFilters()
                    .AnyAsync(x => x.CompanyId == companyId && x.DocumentType == item.DocumentType);
                if (!exists) context.DocumentSequences.Add(item);
            }

            await context.SaveChangesAsync();
        }

        private static decimal GetWholeAmountUpTo50k(Random rng)
        {
            var thousandSteps = rng.Next(1, 51);
            return thousandSteps * 1000m;
        }

        private static PaymentMethod GetPaymentMethod(Random rng)
        {
            return rng.Next(0, 3) switch
            {
                0 => PaymentMethod.Cash,
                1 => PaymentMethod.BankTransfer,
                _ => PaymentMethod.Check
            };
        }

        private static void CreatePasswordHash(string password, out byte[] passwordHash, out byte[] passwordSalt)
        {
            using (var hmac = new HMACSHA512())
            {
                passwordSalt = hmac.Key;
                passwordHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(password));
            }
        }
    }
}

