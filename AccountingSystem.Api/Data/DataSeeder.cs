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
            // Check if any company exists (using IgnoreQueryFilters to see all data)
            if (await context.Companies.IgnoreQueryFilters().AnyAsync()) return;

            // 1. Create Default Company
            var company = new Company
            {
                Name = "Jipos Hardware & Services",
                Address = "Samal City, Davao del Norte",
                TaxId = "TIN-001-002-003",
                Currency = "PHP",
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };
            context.Companies.Add(company);
            await context.SaveChangesAsync(); // Save to generate CompanyId

            // 2. Seed Users Linked to Company
            CreatePasswordHash("admin123", out byte[] adminHash, out byte[] adminSalt);
            CreatePasswordHash("user123", out byte[] userHash, out byte[] userSalt);

            var users = new List<User>
            {
                new User
                {
                    CompanyId = company.Id, // Link to Company
                    Email = "adzyl.jipos@gmail.com",
                    FullName = "Administrator Azi",
                    RoleId = 1,
                    PasswordHash = Convert.ToBase64String(adminHash),
                    PasswordSalt = Convert.ToBase64String(adminSalt),
                    IsActive = true
                },
                new User
                {
                    CompanyId = company.Id,
                    Email = "asi26.dev@gmail.com",
                    FullName = "Accountant Azi",
                    RoleId = 2,
                    PasswordHash = Convert.ToBase64String(userHash),
                    PasswordSalt = Convert.ToBase64String(userSalt),
                    IsActive = true
                },
                new User
                {
                    CompanyId = company.Id,
                    Email = "asi21@gmail.com",
                    FullName = "Manager Azi",
                    RoleId = 3,
                    PasswordHash = Convert.ToBase64String(userHash),
                    PasswordSalt = Convert.ToBase64String(userSalt),
                    IsActive = true
                }
            };
            context.Users.AddRange(users);

            // 3. Chart of Accounts Linked to Company
            var accounts = new List<Account>
            {
                new Account { CompanyId = company.Id, Code = "1000", Name = "Cash on Hand", Type = "Asset" },
                new Account { CompanyId = company.Id, Code = "1010", Name = "BDO Savings", Type = "Asset" },
                new Account { CompanyId = company.Id, Code = "1100", Name = "Accounts Receivable", Type = "Asset" },
                new Account { CompanyId = company.Id, Code = "1200", Name = "Office Equipment", Type = "Asset" },
                new Account { CompanyId = company.Id, Code = "2000", Name = "Accounts Payable", Type = "Liability" },
                new Account { CompanyId = company.Id, Code = "2010", Name = "VAT Payable", Type = "Liability" },
                new Account { CompanyId = company.Id, Code = "3000", Name = "Owner's Capital", Type = "Equity" },
                new Account { CompanyId = company.Id, Code = "3100", Name = "Retained Earnings", Type = "Equity" },
                new Account { CompanyId = company.Id, Code = "4000", Name = "Service Revenue", Type = "Revenue" },
                new Account { CompanyId = company.Id, Code = "4100", Name = "Sales Revenue", Type = "Revenue" },
                new Account { CompanyId = company.Id, Code = "5000", Name = "Rent Expense", Type = "Expense" },
                new Account { CompanyId = company.Id, Code = "5010", Name = "Utilities Expense", Type = "Expense" },
                new Account { CompanyId = company.Id, Code = "5020", Name = "Salaries Expense", Type = "Expense" },
                new Account { CompanyId = company.Id, Code = "5030", Name = "Office Supplies", Type = "Expense" },
                new Account { CompanyId = company.Id, Code = "5040", Name = "Internet & Comm", Type = "Expense" }
            };
            context.Accounts.AddRange(accounts);

            await context.SaveChangesAsync();
        }

        // FIX: Updated to match AuthService logic (HMACSHA512)
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