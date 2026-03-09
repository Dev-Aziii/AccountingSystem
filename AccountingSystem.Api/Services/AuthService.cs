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
        private readonly ICaptchaService _captchaService;
        private readonly ILogger<AuthService> _logger;

        public AuthService(
            AccountingDbContext context,
            IConfiguration configuration,
            ICaptchaService captchaService,
            ILogger<AuthService> logger)
        {
            _context = context;
            _configuration = configuration;
            _captchaService = captchaService;
            _logger = logger;
        }

        // --- Update Profile ---
        public async Task UpdateProfileAsync(int userId, UpdateProfileDTO dto)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null) throw new Exception("User not found.");

            if (user.Email != dto.Email)
            {
                bool emailExists = await _context.Users.AnyAsync(u => u.Email == dto.Email && u.Id != userId);
                if (emailExists) throw new Exception("Email is already in use.");
            }

            user.FullName = dto.FullName;
            user.Email = dto.Email;

            await _context.SaveChangesAsync();
        }

        // --- Change Password ---
        public async Task ChangePasswordAsync(int userId, ChangePasswordDTO dto)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null) throw new Exception("User not found.");

            if (string.IsNullOrEmpty(user.PasswordHash) || string.IsNullOrEmpty(user.PasswordSalt))
                throw new Exception("User password data is corrupted.");

            if (!VerifyPasswordHash(dto.CurrentPassword, Convert.FromBase64String(user.PasswordHash), Convert.FromBase64String(user.PasswordSalt)))
                throw new Exception("Incorrect current password.");

            CreatePasswordHash(dto.NewPassword, out byte[] newHash, out byte[] newSalt);

            user.PasswordHash = Convert.ToBase64String(newHash);
            user.PasswordSalt = Convert.ToBase64String(newSalt);

            await _context.SaveChangesAsync();
        }

        // --- Register Company ---
        public async Task<AuthResponseDTO> RegisterCompanyAsync(CompanyRegisterDTO dto)
        {
            if (!await _captchaService.VerifyTokenAsync(dto.RecaptchaToken))
                throw new Exception("Security check failed. Automated activity detected.");

            if (await _context.Users.IgnoreQueryFilters().AnyAsync(u => u.Email == dto.AdminEmail))
                throw new Exception("Email already exists.");

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var company = new Company
                {
                    Name = dto.CompanyName,
                    CreatedAt = DateTime.UtcNow,
                    IsActive = true,
                    Currency = "PHP",
                    FiscalYearStartMonth = 1
                };
                _context.Companies.Add(company);
                await _context.SaveChangesAsync();

                var adminRole = await _context.Roles.FirstOrDefaultAsync(r => r.Name == "Admin");
                if (adminRole == null) throw new Exception("System Role 'Admin' missing.");

                CreatePasswordHash(dto.Password, out byte[] passwordHash, out byte[] passwordSalt);

                var user = new User
                {
                    CompanyId = company.Id,
                    Email = dto.AdminEmail,
                    FullName = dto.AdminFullName,
                    RoleId = adminRole.Id,
                    Role = adminRole,
                    PasswordHash = Convert.ToBase64String(passwordHash),
                    PasswordSalt = Convert.ToBase64String(passwordSalt),
                    IsActive = true
                };
                _context.Users.Add(user);
                await SeedCompanyDataAsync(company.Id);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                var expiryMinutes = int.Parse(_configuration["JwtSettings:ExpiryMinutes"] ?? "60");

                var token = GenerateJwtToken(user, company);
                return new AuthResponseDTO
                {
                    Token = token,
                    Email = user.Email,
                    Role = "Admin",
                    CompanyId = company.Id,
                    CompanyName = company.Name,
                    ExpiresAt = DateTime.UtcNow.AddMinutes(expiryMinutes)
                };
            }
            catch (DbUpdateException ex)
            {
                await transaction.RollbackAsync();
                var databaseError = ex.InnerException?.Message ?? ex.Message;
                _logger.LogError(
                    ex,
                    "RegisterCompany failed for email {AdminEmail} and company {CompanyName}. Database error: {DatabaseError}",
                    dto.AdminEmail,
                    dto.CompanyName,
                    databaseError);
                throw new Exception("Registration failed while saving your company account. Please try again.");
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        // --- Register User ---
        public async Task<User> RegisterAsync(RegisterDTO registerDto)
        {
            if (await _context.Users.AnyAsync(u => u.Email == registerDto.Email))
                throw new Exception("Email already exists in this company.");

            var normalizedRoleName = registerDto.RoleName.Trim();
            var role = await _context.Roles
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Name.ToLower() == normalizedRoleName.ToLower());

            if (role == null)
                throw new Exception($"Role '{registerDto.RoleName}' does not exist.");

            if (role.Name == "SuperAdmin")
                throw new Exception("SuperAdmin role cannot be assigned from this endpoint.");

            CreatePasswordHash(registerDto.Password, out byte[] passwordHash, out byte[] passwordSalt);

            var user = new User
            {
                Email = registerDto.Email,
                FullName = registerDto.FullName,
                RoleId = role.Id,
                PasswordHash = Convert.ToBase64String(passwordHash),
                PasswordSalt = Convert.ToBase64String(passwordSalt),
                IsActive = true
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();
            return user;
        }

        // --- Login ---
        public async Task<AuthResponseDTO> LoginAsync(LoginDTO loginDto)
        {
            var user = await _context.Users
                .IgnoreQueryFilters()
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.Email == loginDto.Email);

            if (user == null || user.IsDeleted)
                throw new Exception("Invalid email or password.");

            if (user.Status == "Blocked")
                throw new Exception("Your account has been blocked. Please contact the System Administrator.");

            if (!user.IsActive)
                throw new Exception("Your account has been deactivated. Please contact your administrator.");

            if (string.IsNullOrEmpty(user.PasswordHash) || string.IsNullOrEmpty(user.PasswordSalt))
                throw new Exception("User password data is corrupted.");

            if (!VerifyPasswordHash(loginDto.Password, Convert.FromBase64String(user.PasswordHash), Convert.FromBase64String(user.PasswordSalt)))
                throw new Exception("Invalid email or password.");

            var company = await _context.Companies.IgnoreQueryFilters().FirstOrDefaultAsync(c => c.Id == user.CompanyId);
            if (company == null) throw new Exception("Company data not found.");

            if (user.Role.Name != "SuperAdmin")
            {
                if (company.Status == "Blocked")
                    throw new Exception("This organization has been permanently blocked. Please contact the System Owner.");
                if (company.Status == "Suspended" || !company.IsActive)
                    throw new Exception("This organization's access has been suspended. Please contact the System Owner.");
            }

            var expiryMinutes = int.Parse(_configuration["JwtSettings:ExpiryMinutes"] ?? "60");

            var token = GenerateJwtToken(user, company);
            return new AuthResponseDTO
            {
                Token = token,
                Email = user.Email,
                Role = user.Role.Name,
                CompanyId = company.Id,
                CompanyName = company.Name,
                ExpiresAt = DateTime.UtcNow.AddMinutes(expiryMinutes)
            };
        }

        // --- Helpers ---
        private async Task SeedCompanyDataAsync(int companyId)
        {
            var accounts = new List<Account>
            {
                new() { CompanyId = companyId, Code = "1000", Name = "Cash on Hand",        Type = "Asset"     },
                new() { CompanyId = companyId, Code = "1010", Name = "Bank",                Type = "Asset"     },
                new() { CompanyId = companyId, Code = "1100", Name = "Accounts Receivable", Type = "Asset"     },
                new() { CompanyId = companyId, Code = "2000", Name = "Accounts Payable",    Type = "Liability" },
                new() { CompanyId = companyId, Code = "3000", Name = "Owner's Capital",     Type = "Equity"    },
                new() { CompanyId = companyId, Code = "4000", Name = "Sales Revenue",       Type = "Revenue"   },
                new() { CompanyId = companyId, Code = "5000", Name = "General Expense",     Type = "Expense"   }
            };

            _context.Accounts.AddRange(accounts);
            await _context.SaveChangesAsync();
        }

        private static void CreatePasswordHash(string password, out byte[] passwordHash, out byte[] passwordSalt)
        {
            using var hmac = new HMACSHA512();
            passwordSalt = hmac.Key;
            passwordHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(password));
        }

        private static bool VerifyPasswordHash(string password, byte[] storedHash, byte[] storedSalt)
        {
            using var hmac = new HMACSHA512(storedSalt);
            var computedHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(password));
            for (int i = 0; i < computedHash.Length; i++)
            {
                if (computedHash[i] != storedHash[i]) return false;
            }
            return true;
        }

        private string GenerateJwtToken(User user, Company company)
        {
            var key = Encoding.ASCII.GetBytes(_configuration["JwtSettings:Secret"]!);
            var tokenHandler = new JwtSecurityTokenHandler();
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.Name,  user.Email),
                    new Claim(ClaimTypes.Role,  user.Role.Name),
                    new Claim("UserId",         user.Id.ToString()),
                    new Claim("role",           user.Role.Name),
                    new Claim("FullName",       user.FullName ?? user.Email),
                    new Claim("CompanyId",      company.Id.ToString()),
                    new Claim("CompanyName",    company.Name)
                }),
                Expires = DateTime.UtcNow.AddMinutes(double.Parse(_configuration["JwtSettings:ExpiryMinutes"] ?? "60")),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature),
                Issuer = _configuration["JwtSettings:Issuer"],
                Audience = _configuration["JwtSettings:Audience"]
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }
    }
}
