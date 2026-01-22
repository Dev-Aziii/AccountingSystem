using AccountingSystem.API.Data;
using AccountingSystem.API.Services.Interfaces;
using AccountingSystem.Shared.DTOs;
using AccountingSystem.API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace AccountingSystem.API.Services
{
    public class AuthService : IAuthService
    {
        private readonly AccountingDbContext _context;
        private readonly IConfiguration _configuration;

        public AuthService(AccountingDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        // --- NEW: Multi-Tenant Registration ---
        public async Task<AuthResponseDTO> RegisterCompanyAsync(CompanyRegisterDTO dto)
        {
            // 1. Check Global Email Uniqueness
            // We use IgnoreQueryFilters because we need to check ALL companies
            if (await _context.Users.IgnoreQueryFilters().AnyAsync(u => u.Email == dto.AdminEmail))
                throw new Exception("Email already exists.");

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // 2. Create Company
                var company = new Company
                {
                    Name = dto.CompanyName,
                    CreatedAt = DateTime.UtcNow,
                    IsActive = true,
                    Currency = "PHP"
                };
                _context.Companies.Add(company);
                await _context.SaveChangesAsync(); // Generates Company.Id

                // 3. Create Admin User linked to Company
                var adminRole = await _context.Roles.FirstOrDefaultAsync(r => r.Name == "Admin");
                if (adminRole == null) throw new Exception("System Role 'Admin' missing.");

                CreatePasswordHash(dto.Password, out byte[] passwordHash, out byte[] passwordSalt);

                var user = new User
                {
                    CompanyId = company.Id, // Explicit Assignment
                    Email = dto.AdminEmail,
                    FullName = dto.AdminFullName,
                    RoleId = adminRole.Id,
                    PasswordHash = Convert.ToBase64String(passwordHash),
                    PasswordSalt = Convert.ToBase64String(passwordSalt),
                    IsActive = true
                };
                _context.Users.Add(user);

                // 4. Seed Default Data for this Company (Chart of Accounts)
                await SeedCompanyDataAsync(company.Id);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                // 5. Auto-Login
                var token = GenerateJwtToken(user, company);
                return new AuthResponseDTO
                {
                    Token = token,
                    Email = user.Email,
                    Role = "Admin",
                    CompanyId = company.Id,
                    CompanyName = company.Name,
                    ExpiresAt = DateTime.UtcNow.AddMinutes(int.Parse(_configuration["JwtSettings:ExpiryMinutes"]))
                };
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<User> RegisterAsync(RegisterDTO registerDto)
        {
            // Note: This method is for adding users to an EXISTING company (by an Admin)
            // The DbContext automatically handles CompanyId via TenantService for the logged-in Admin

            if (await _context.Users.AnyAsync(u => u.Email == registerDto.Email))
                throw new Exception("Email already exists in this company.");

            var role = await _context.Roles.FirstOrDefaultAsync(r => r.Name == registerDto.RoleName);
            if (role == null) throw new Exception($"Role '{registerDto.RoleName}' does not exist.");

            CreatePasswordHash(registerDto.Password, out byte[] passwordHash, out byte[] passwordSalt);

            var user = new User
            {
                Email = registerDto.Email,
                FullName = registerDto.FullName,
                RoleId = role.Id,
                PasswordHash = Convert.ToBase64String(passwordHash),
                PasswordSalt = Convert.ToBase64String(passwordSalt),
                IsActive = true
                // CompanyId is set automatically by DbContext.SaveChanges override based on current Admin
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();
            return user;
        }

        public async Task<AuthResponseDTO> LoginAsync(LoginDTO loginDto)
        {
            // CRITICAL: We must IgnoreQueryFilters because the user is not logged in yet,
            // so the TenantService returns 0, filtering out all users.
            var user = await _context.Users
                .IgnoreQueryFilters()
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.Email == loginDto.Email);

            if (user == null || user.IsDeleted || !user.IsActive)
                throw new Exception("Invalid email or password.");

            if (!VerifyPasswordHash(loginDto.Password, Convert.FromBase64String(user.PasswordHash), Convert.FromBase64String(user.PasswordSalt)))
                throw new Exception("Invalid email or password.");

            // Fetch Company Info manually since Filters are ignored
            var company = await _context.Companies.FindAsync(user.CompanyId);
            if (company == null || !company.IsActive)
                throw new Exception("Company account is inactive.");

            var token = GenerateJwtToken(user, company);

            return new AuthResponseDTO
            {
                Token = token,
                Email = user.Email,
                Role = user.Role.Name,
                CompanyId = company.Id,
                CompanyName = company.Name,
                ExpiresAt = DateTime.UtcNow.AddMinutes(int.Parse(_configuration["JwtSettings:ExpiryMinutes"]))
            };
        }

        // --- Helpers ---

        private async Task SeedCompanyDataAsync(int companyId)
        {
            var accounts = new List<Account>
            {
                new Account { CompanyId = companyId, Code = "1000", Name = "Cash on Hand", Type = "Asset" },
                new Account { CompanyId = companyId, Code = "1010", Name = "Bank", Type = "Asset" },
                new Account { CompanyId = companyId, Code = "1100", Name = "Accounts Receivable", Type = "Asset" },
                new Account { CompanyId = companyId, Code = "2000", Name = "Accounts Payable", Type = "Liability" },
                new Account { CompanyId = companyId, Code = "3000", Name = "Owner's Capital", Type = "Equity" },
                new Account { CompanyId = companyId, Code = "4000", Name = "Sales Revenue", Type = "Revenue" },
                new Account { CompanyId = companyId, Code = "5000", Name = "General Expense", Type = "Expense" }
            };
            _context.Accounts.AddRange(accounts);
            // Add initial Partners? Optional.
        }

        private void CreatePasswordHash(string password, out byte[] passwordHash, out byte[] passwordSalt)
        {
            using (var hmac = new HMACSHA512())
            {
                passwordSalt = hmac.Key;
                passwordHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(password));
            }
        }

        private bool VerifyPasswordHash(string password, byte[] storedHash, byte[] storedSalt)
        {
            using (var hmac = new HMACSHA512(storedSalt))
            {
                var computedHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(password));
                for (int i = 0; i < computedHash.Length; i++)
                {
                    if (computedHash[i] != storedHash[i]) return false;
                }
            }
            return true;
        }

        private string GenerateJwtToken(User user, Company company)
        {
            var key = Encoding.ASCII.GetBytes(_configuration["JwtSettings:Secret"]);
            var tokenHandler = new JwtSecurityTokenHandler();
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.Name, user.Email),
                    new Claim(ClaimTypes.Role, user.Role.Name),
                    new Claim("UserId", user.Id.ToString()),
                    new Claim("role", user.Role.Name),
                    new Claim("FullName", user.FullName ?? user.Email),
                    
                    // NEW: Multi-Tenant Claims
                    new Claim("CompanyId", company.Id.ToString()),
                    new Claim("CompanyName", company.Name)
                }),
                Expires = DateTime.UtcNow.AddMinutes(double.Parse(_configuration["JwtSettings:ExpiryMinutes"])),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature),
                Issuer = _configuration["JwtSettings:Issuer"],
                Audience = _configuration["JwtSettings:Audience"]
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }
    }
}