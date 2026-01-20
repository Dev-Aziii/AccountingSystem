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
            // Check if data already exists to avoid duplication
            if (await context.Users.AnyAsync()) return;

            // 1. Seed Users (Password: admin123)
            // Note: Roles are already seeded in DbContext.OnModelCreating (1=Admin, 2=Accounting, 3=Management)
            var users = new List<User>
            {
                new User
                {
                    Username = "admin",
                    FullName = "System Administrator",
                    RoleId = 1,
                    PasswordHash = HashPassword("admin123")
                },
                new User
                {
                    Username = "accountant",
                    FullName = "Maria Santos",
                    RoleId = 2,
                    PasswordHash = HashPassword("user123")
                },
                new User
                {
                    Username = "manager",
                    FullName = "John Manager",
                    RoleId = 3,
                    PasswordHash = HashPassword("user123")
                }
            };
            context.Users.AddRange(users);

            // 2. Seed Chart of Accounts
            var accounts = new List<Account>
            {
                // Assets (1000-1999)
                new Account { Code = "1000", Name = "Cash on Hand", Type = "Asset" },
                new Account { Code = "1010", Name = "BDO Savings", Type = "Asset" },
                new Account { Code = "1020", Name = "Petty Cash", Type = "Asset" },
                new Account { Code = "1100", Name = "Accounts Receivable", Type = "Asset" },
                new Account { Code = "1200", Name = "Office Equipment", Type = "Asset" },

                // Liabilities (2000-2999)
                new Account { Code = "2000", Name = "Accounts Payable", Type = "Liability" },
                new Account { Code = "2010", Name = "VAT Payable", Type = "Liability" },
                new Account { Code = "2020", Name = "SSS Payable", Type = "Liability" },

                // Equity (3000-3999)
                new Account { Code = "3000", Name = "Owner's Capital", Type = "Equity" },
                new Account { Code = "3100", Name = "Retained Earnings", Type = "Equity" },

                // Revenue (4000-4999)
                new Account { Code = "4000", Name = "Service Revenue", Type = "Revenue" },
                new Account { Code = "4100", Name = "Sales Revenue", Type = "Revenue" },
                new Account { Code = "4200", Name = "Interest Income", Type = "Revenue" },

                // Expenses (5000-5999)
                new Account { Code = "5000", Name = "Rent Expense", Type = "Expense" },
                new Account { Code = "5010", Name = "Utilities Expense", Type = "Expense" },
                new Account { Code = "5020", Name = "Salaries Expense", Type = "Expense" },
                new Account { Code = "5030", Name = "Office Supplies", Type = "Expense" },
                new Account { Code = "5040", Name = "Internet & Comm", Type = "Expense" }
            };
            context.Accounts.AddRange(accounts);

            // 3. Seed Partners
            context.Vendors.AddRange(
                new Vendor { Name = "Meralco", Email = "bills@meralco.com.ph", ContactPerson = "Billing Dept" },
                new Vendor { Name = "PLDT", Email = "help@pldt.com", ContactPerson = "Enterprise Support" },
                new Vendor { Name = "Water District", Email = "bills@water.com.ph", ContactPerson = "Sales Rep" }
            );

            context.Customers.AddRange(
                new Customer { Name = "Acme Corp", Email = "finance@acme.com", Phone = "0917-000-1111" },
                new Customer { Name = "Mega Build Inc", Email = "purchase@megabuild.com", Phone = "0918-222-3333" },
                new Customer { Name = "Juan Dela Cruz", Email = "juan@gmail.com", Phone = "0919-444-5555" }
            );

            /* 4. Initial Opening Balance (Optional)
            // Simple Journal Entry to start with some Cash and Equity
            var openingEntry = new JournalEntry
            {
                Date = DateTime.UtcNow.AddDays(-1),
                Description = "Opening Balance",
                Reference = "OB-001",
                CreatedBy = "System",
                IsPosted = true,
                Lines = new List<JournalEntryLine>
                {
                    new JournalEntryLine { Account = accounts.First(a => a.Code == "1000"), Debit = 50000, Credit = 0 }, // Cash
                    new JournalEntryLine { Account = accounts.First(a => a.Code == "3000"), Debit = 0, Credit = 50000 }  // Equity
                }
            };
            context.JournalEntries.Add(openingEntry);
            */

            await context.SaveChangesAsync();
        }

        private static string HashPassword(string password)
        {
            using (var sha256 = SHA256.Create())
            {
                var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
                return Convert.ToBase64String(bytes);
            }
        }
    }
}