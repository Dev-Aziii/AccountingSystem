using AccountingSystem.API.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace AccountingSystem.API.Data
{
    public static class DataSeeder
    {
        public static async Task SeedDataAsync(AccountingDbContext context)
        {
            var superEmail = "sysadmin@accsys.com";
            if (!await context.Users.IgnoreQueryFilters().AnyAsync(u => u.Email == superEmail))
            {
                var hostCompany = new Company { Name = "SaaS Operations", Address = "HQ", TaxId = "000", Currency = "PHP", CreatedAt = DateTime.UtcNow, IsActive = true };
                context.Companies.Add(hostCompany);
                await context.SaveChangesAsync();
                CreatePasswordHash("master123", out byte[] h, out byte[] s);
                context.Users.Add(new User { CompanyId = hostCompany.Id, Email = superEmail, FullName = "System Owner", RoleId = 4, PasswordHash = Convert.ToBase64String(h), PasswordSalt = Convert.ToBase64String(s), IsActive = true });
                await context.SaveChangesAsync();
            }

            if (await context.Companies.IgnoreQueryFilters().CountAsync() < 2)
            {
                var company = new Company { Name = "Jipos Hardware & Services", Address = "123 Innovation Drive, Tech City", TaxId = "TIN-001-002-003", Currency = "PHP", CreatedAt = DateTime.UtcNow, IsActive = true };
                context.Companies.Add(company);
                await context.SaveChangesAsync();

                CreatePasswordHash("admin123", out byte[] adminHash, out byte[] adminSalt);
                CreatePasswordHash("user123", out byte[] userHash, out byte[] userSalt);
                context.Users.AddRange(
                    new User { CompanyId = company.Id, Email = "superadmin@accsys.com", FullName = "System Administrator", RoleId = 1, PasswordHash = Convert.ToBase64String(adminHash), PasswordSalt = Convert.ToBase64String(adminSalt), IsActive = true },
                    new User { CompanyId = company.Id, Email = "accountant@accsys.com", FullName = "Maria Santos", RoleId = 2, PasswordHash = Convert.ToBase64String(userHash), PasswordSalt = Convert.ToBase64String(userSalt), IsActive = true },
                    new User { CompanyId = company.Id, Email = "manager@accsys.com", FullName = "John Manager", RoleId = 3, PasswordHash = Convert.ToBase64String(userHash), PasswordSalt = Convert.ToBase64String(userSalt), IsActive = true }
                );

                context.Accounts.AddRange(new List<Account>
                {
                    new() { CompanyId = company.Id, Code = "1000", Name = "Cash on Hand", Type = "Asset" },
                    new() { CompanyId = company.Id, Code = "1010", Name = "BDO Savings", Type = "Asset" },
                    new() { CompanyId = company.Id, Code = "1100", Name = "Accounts Receivable", Type = "Asset" },
                    new() { CompanyId = company.Id, Code = "2000", Name = "Accounts Payable", Type = "Liability" },
                    new() { CompanyId = company.Id, Code = "3000", Name = "Owner's Capital", Type = "Equity" },
                    new() { CompanyId = company.Id, Code = "3100", Name = "Retained Earnings", Type = "Equity" },
                    new() { CompanyId = company.Id, Code = "4000", Name = "Service Revenue", Type = "Revenue" },
                    new() { CompanyId = company.Id, Code = "4100", Name = "Sales Revenue", Type = "Revenue" },
                    new() { CompanyId = company.Id, Code = "5000", Name = "Rent Expense", Type = "Expense" },
                    new() { CompanyId = company.Id, Code = "5010", Name = "Utilities Expense", Type = "Expense" },
                    new() { CompanyId = company.Id, Code = "5020", Name = "Salaries Expense", Type = "Expense" }
                });
                await context.SaveChangesAsync();

                var fy2024 = new FiscalYear { CompanyId = company.Id, Name = "FY2024", StartDate = new DateTime(2024, 1, 1), EndDate = new DateTime(2024, 12, 31), IsClosed = true, ClosedAt = DateTime.UtcNow };
                var fy2025 = new FiscalYear { CompanyId = company.Id, Name = "FY2025", StartDate = new DateTime(2025, 1, 1), EndDate = new DateTime(2025, 12, 31), IsClosed = true, ClosedAt = DateTime.UtcNow };
                var fy2026 = new FiscalYear { CompanyId = company.Id, Name = "FY2026", StartDate = new DateTime(2026, 1, 1), EndDate = new DateTime(2026, 12, 31), IsClosed = false };
                context.FiscalYears.AddRange(fy2024, fy2025, fy2026);
                await context.SaveChangesAsync();

                var accounts = await context.Accounts.Where(a => a.CompanyId == company.Id).ToDictionaryAsync(a => a.Code, a => a.Id);
                var journals = new List<JournalEntry>();
                for (var y = 2024; y <= 2026; y++)
                {
                    for (var i = 1; i <= 10; i++)
                    {
                        var amount = 500 + (i * 25);
                        journals.Add(new JournalEntry
                        {
                            CompanyId = company.Id,
                            Date = new DateTime(y, (i % 12) + 1, 5),
                            Description = $"Sales {y}-{i}",
                            Reference = $"SALE-{y}-{i}",
                            IsPosted = true,
                            Lines = new()
                            {
                                new JournalEntryLine{ AccountId = accounts["1100"], Debit = amount },
                                new JournalEntryLine{ AccountId = accounts["4000"], Credit = amount }
                            }
                        });
                        journals.Add(new JournalEntry
                        {
                            CompanyId = company.Id,
                            Date = new DateTime(y, (i % 12) + 1, 20),
                            Description = $"Expense {y}-{i}",
                            Reference = $"EXP-{y}-{i}",
                            IsPosted = true,
                            Lines = new()
                            {
                                new JournalEntryLine{ AccountId = accounts["5000"], Debit = amount * 0.4m },
                                new JournalEntryLine{ AccountId = accounts["2000"], Credit = amount * 0.4m }
                            }
                        });
                    }
                }
                journals.Add(new JournalEntry { CompanyId = company.Id, Date = new DateTime(2024, 12, 31), Description = "FY2024 Close", Reference = "FY-CLOSE-FY2024", IsPosted = true, IsSystemGenerated = true, Lines = new() { new JournalEntryLine { AccountId = accounts["4000"], Debit = 5000 }, new JournalEntryLine { AccountId = accounts["5000"], Credit = 2000 }, new JournalEntryLine { AccountId = accounts["3100"], Credit = 3000 } } });
                journals.Add(new JournalEntry { CompanyId = company.Id, Date = new DateTime(2025, 12, 31), Description = "FY2025 Close", Reference = "FY-CLOSE-FY2025", IsPosted = true, IsSystemGenerated = true, Lines = new() { new JournalEntryLine { AccountId = accounts["4000"], Debit = 6000 }, new JournalEntryLine { AccountId = accounts["5000"], Credit = 2500 }, new JournalEntryLine { AccountId = accounts["3100"], Credit = 3500 } } });
                journals.Add(new JournalEntry { CompanyId = company.Id, Date = new DateTime(2026, 1, 1), Description = "FY2026 Opening", Reference = "FY-OPEN-FY2026", IsPosted = true, IsSystemGenerated = true, Lines = new() { new JournalEntryLine { AccountId = accounts["1100"], Debit = 10000 }, new JournalEntryLine { AccountId = accounts["2000"], Credit = 2500 }, new JournalEntryLine { AccountId = accounts["3100"], Credit = 7500 } } });
                context.JournalEntries.AddRange(journals);

                var customer = new Customer { CompanyId = company.Id, Name = "ABC Corp", Email = "ap@abc.com" };
                var vendor = new Vendor { CompanyId = company.Id, Name = "Supply Co", Email = "sales@supply.co" };
                context.Customers.Add(customer);
                context.Vendors.Add(vendor);
                await context.SaveChangesAsync();

                for (var y = 2024; y <= 2026; y++)
                {
                    for (var i = 1; i <= 5; i++)
                    {
                        context.Invoices.Add(new Invoice { CompanyId = company.Id, CustomerId = customer.Id, DueDate = new DateTime(y, i, 15), TotalAmount = 1000 + i * 100, PaidAmount = i % 2 == 0 ? 1000 + i * 100 : 400, Status = i % 2 == 0 ? Shared.Enums.DocumentStatus.Paid : Shared.Enums.DocumentStatus.PartiallyPaid });
                        context.Bills.Add(new Bill { CompanyId = company.Id, VendorId = vendor.Id, DueDate = new DateTime(y, i, 18), Amount = 600 + i * 80, AmountPaid = i % 2 == 0 ? 600 + i * 80 : 200, Status = i % 2 == 0 ? Shared.Enums.DocumentStatus.Paid : Shared.Enums.DocumentStatus.PartiallyPaid, ReferenceNumber = $"B-{y}-{i}" });
                    }
                }

                await context.SaveChangesAsync();
            }
        }

        private static void CreatePasswordHash(string password, out byte[] passwordHash, out byte[] passwordSalt)
        {
            using var hmac = new HMACSHA512();
            passwordSalt = hmac.Key;
            passwordHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(password));
        }
    }
}
