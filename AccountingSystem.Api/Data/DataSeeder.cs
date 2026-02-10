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
            // --- 1. SEED SUPER ADMIN (The SaaS Owner) ---
            // Check if Super Admin exists (Ignore filters to check globally)
            var superEmail = "sysadmin@accsys.com";
            if (!await context.Users.IgnoreQueryFilters().AnyAsync(u => u.Email == superEmail))
            {
                // Create Host Company (The SaaS Provider Entity)
                var hostCompany = new Company
                {
                    Name = "SaaS Operations",
                    Address = "HQ",
                    TaxId = "000",
                    Currency = "PHP",
                    CreatedAt = DateTime.UtcNow,
                    IsActive = true
                };
                context.Companies.Add(hostCompany);
                await context.SaveChangesAsync();

                // Create Super Admin User
                CreatePasswordHash("master123", out byte[] h, out byte[] s);
                var superUser = new User
                {
                    CompanyId = hostCompany.Id,
                    Email = superEmail,
                    FullName = "System Owner",
                    RoleId = 4, // SuperAdmin Role
                    PasswordHash = Convert.ToBase64String(h),
                    PasswordSalt = Convert.ToBase64String(s),
                    IsActive = true
                };
                context.Users.Add(superUser);
                await context.SaveChangesAsync();
            }

            // --- 2. SEED DEFAULT TENANT (Demo Company) ---
            // Only if no other companies exist (besides the host we potentially just created)
            // We check if count < 2 (Host + 1 Demo)
            if (await context.Companies.IgnoreQueryFilters().CountAsync() < 2)
            {
                var company = new Company
                {
                    Name = "Jipos Hardware & Services",
                    Address = "123 Innovation Drive, Tech City",
                    TaxId = "TIN-001-002-003",
                    Currency = "PHP",
                    CreatedAt = DateTime.UtcNow,
                    IsActive = true
                };
                context.Companies.Add(company);
                await context.SaveChangesAsync();

                CreatePasswordHash("admin123", out byte[] adminHash, out byte[] adminSalt);
                CreatePasswordHash("user123", out byte[] userHash, out byte[] userSalt);

                var users = new List<User>
                {
                    new User
                    {
                        CompanyId = company.Id,
                        Email = "superadmin@accsys.com",
                        FullName = "System Administrator",
                        RoleId = 1,
                        PasswordHash = Convert.ToBase64String(adminHash),
                        PasswordSalt = Convert.ToBase64String(adminSalt),
                        IsActive = true
                    },
                    new User
                    {
                        CompanyId = company.Id,
                        Email = "accountant@accsys.com",
                        FullName = "Maria Santos",
                        RoleId = 2,
                        PasswordHash = Convert.ToBase64String(userHash),
                        PasswordSalt = Convert.ToBase64String(userSalt),
                        IsActive = true
                    },
                    new User
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