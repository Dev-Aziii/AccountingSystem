using AccountingSystem.API.Configuration;
using AccountingSystem.API.Data;
using AccountingSystem.API.Security;
using AccountingSystem.API.Models;
using AccountingSystem.API.Services.Interfaces;
using AccountingSystem.Shared.DTOs;
using AccountingSystem.Shared.Validation;
using Microsoft.EntityFrameworkCore;

namespace AccountingSystem.API.Services
{
    public class AuthService : IAuthService
    {
        private const int DefaultMaxFailedAccessAttempts = 5;
        private const int DefaultLockoutMinutes = 15;

        private readonly AccountingDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly ICaptchaService _captchaService;
        private readonly ILogger<AuthService> _logger;
        private readonly IAuthSecurityAuditService _auditService;
        private readonly ILegacyPasswordService _legacyPasswordService;
        private readonly IAuthTokenFactory _authTokenFactory;

        public AuthService(
            AccountingDbContext context,
            IConfiguration configuration,
            ICaptchaService captchaService,
            ILogger<AuthService> logger,
            IAuthSecurityAuditService auditService,
            ILegacyPasswordService legacyPasswordService,
            IAuthTokenFactory authTokenFactory)
        {
            _context = context;
            _configuration = configuration;
            _captchaService = captchaService;
            _logger = logger;
            _auditService = auditService;
            _legacyPasswordService = legacyPasswordService;
            _authTokenFactory = authTokenFactory;
        }

        public async Task UpdateProfileAsync(int userId, UpdateProfileDTO dto)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                await _auditService.WriteAsync("AUTH-PROFILE-UPDATE-FAILURE", userId: userId, reason: "UserNotFound");
                throw new Exception("User not found.");
            }

            var emailChanged = !string.Equals(user.Email, dto.Email, StringComparison.OrdinalIgnoreCase);
            if (emailChanged)
            {
                var emailExists = await _context.Users.AnyAsync(u => u.Email == dto.Email && u.Id != userId);
                if (emailExists)
                {
                    await _auditService.WriteAsync(
                        "AUTH-PROFILE-UPDATE-FAILURE",
                        userId: user.Id,
                        companyId: user.CompanyId,
                        email: user.Email,
                        reason: "EmailAlreadyInUse");
                    throw new Exception("Email is already in use.");
                }
            }

            user.FullName = dto.FullName;
            user.Email = dto.Email;

            await _context.SaveChangesAsync();
            await _auditService.WriteAsync(
                "AUTH-PROFILE-UPDATE",
                userId: user.Id,
                companyId: user.CompanyId,
                email: user.Email,
                reason: emailChanged ? "EmailChanged" : "ProfileUpdated");
        }

        public async Task ChangePasswordAsync(int userId, ChangePasswordDTO dto)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                await _auditService.WriteAsync("AUTH-PASSWORD-CHANGE-FAILURE", userId: userId, reason: "UserNotFound");
                throw new Exception("User not found.");
            }

            if (!_legacyPasswordService.TryVerify(dto.CurrentPassword, user.PasswordHash, user.PasswordSalt, out var currentPasswordMatches))
            {
                await _auditService.WriteAsync(
                    "AUTH-PASSWORD-CHANGE-FAILURE",
                    userId: user.Id,
                    companyId: user.CompanyId,
                    email: user.Email,
                    reason: "PasswordDataCorrupted");
                throw new Exception("User password data is corrupted.");
            }

            if (!currentPasswordMatches)
            {
                await _auditService.WriteAsync(
                    "AUTH-PASSWORD-CHANGE-FAILURE",
                    userId: user.Id,
                    companyId: user.CompanyId,
                    email: user.Email,
                    reason: "InvalidCurrentPassword");
                throw new Exception("Incorrect current password.");
            }

            if (!PasswordPolicy.TryValidate(dto.NewPassword, out var passwordValidationMessage))
            {
                await _auditService.WriteAsync(
                    "AUTH-PASSWORD-CHANGE-FAILURE",
                    userId: user.Id,
                    companyId: user.CompanyId,
                    email: user.Email,
                    reason: "WeakPassword");
                throw new Exception(passwordValidationMessage);
            }

            var newPasswordData = _legacyPasswordService.CreateHash(dto.NewPassword);
            user.PasswordHash = newPasswordData.PasswordHash;
            user.PasswordSalt = newPasswordData.PasswordSalt;

            await _context.SaveChangesAsync();
            await _auditService.WriteAsync(
                "AUTH-PASSWORD-CHANGE",
                userId: user.Id,
                companyId: user.CompanyId,
                email: user.Email,
                reason: "PasswordUpdated");
        }

        public async Task<AuthResponseDTO> RegisterCompanyAsync(CompanyRegisterDTO dto)
        {
            if (!await _captchaService.VerifyTokenAsync(dto.RecaptchaToken))
            {
                await _auditService.WriteAsync(
                    "AUTH-REGISTER-COMPANY-FAILURE",
                    email: dto.AdminEmail,
                    reason: "CaptchaVerificationFailed");
                throw new Exception("Security check failed. Automated activity detected.");
            }

            if (!PasswordPolicy.TryValidate(dto.Password, out var passwordValidationMessage))
            {
                await _auditService.WriteAsync(
                    "AUTH-REGISTER-COMPANY-FAILURE",
                    email: dto.AdminEmail,
                    reason: "WeakPassword");
                throw new Exception(passwordValidationMessage);
            }

            if (await _context.Users.IgnoreQueryFilters().AnyAsync(u => u.Email == dto.AdminEmail))
            {
                await _auditService.WriteAsync(
                    "AUTH-REGISTER-COMPANY-FAILURE",
                    email: dto.AdminEmail,
                    reason: "EmailAlreadyExists");
                throw new Exception("Email already exists.");
            }

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
                if (adminRole == null)
                {
                    await _auditService.WriteAsync(
                        "AUTH-REGISTER-COMPANY-FAILURE",
                        companyId: company.Id,
                        email: dto.AdminEmail,
                        reason: "AdminRoleMissing");
                    throw new Exception("System Role 'Admin' missing.");
                }

                var passwordData = _legacyPasswordService.CreateHash(dto.Password);

                var user = new User
                {
                    CompanyId = company.Id,
                    Email = dto.AdminEmail,
                    FullName = dto.AdminFullName,
                    RoleId = adminRole.Id,
                    Role = adminRole,
                    PasswordHash = passwordData.PasswordHash,
                    PasswordSalt = passwordData.PasswordSalt,
                    IsActive = true
                };
                _context.Users.Add(user);
                await SeedCompanyDataAsync(company.Id);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                var tokenResult = _authTokenFactory.Create(CreateTokenContext(user, company));
                await _auditService.WriteAsync(
                    "AUTH-REGISTER-COMPANY",
                    userId: user.Id,
                    companyId: company.Id,
                    email: user.Email,
                    reason: "Success");

                return new AuthResponseDTO
                {
                    Token = tokenResult.Token,
                    Email = user.Email,
                    Role = "Admin",
                    CompanyId = company.Id,
                    CompanyName = company.Name,
                    ExpiresAt = tokenResult.ExpiresAt
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
                await _auditService.WriteAsync(
                    "AUTH-REGISTER-COMPANY-FAILURE",
                    email: dto.AdminEmail,
                    reason: "DatabaseError");
                throw new Exception("Registration failed while saving your company account. Please try again.");
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<User> RegisterAsync(RegisterDTO registerDto)
        {
            if (!PasswordPolicy.TryValidate(registerDto.Password, out var passwordValidationMessage))
            {
                throw new Exception(passwordValidationMessage);
            }

            if (await _context.Users.AnyAsync(u => u.Email == registerDto.Email))
            {
                throw new Exception("Email already exists in this company.");
            }

            var normalizedRoleName = registerDto.RoleName.Trim();
            var role = await _context.Roles
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Name.ToLower() == normalizedRoleName.ToLower());

            if (role == null)
            {
                throw new Exception($"Role '{registerDto.RoleName}' does not exist.");
            }

            if (role.Name == "SuperAdmin")
            {
                throw new Exception("SuperAdmin role cannot be assigned from this endpoint.");
            }

            var passwordData = _legacyPasswordService.CreateHash(registerDto.Password);

            var user = new User
            {
                Email = registerDto.Email,
                FullName = registerDto.FullName,
                RoleId = role.Id,
                PasswordHash = passwordData.PasswordHash,
                PasswordSalt = passwordData.PasswordSalt,
                IsActive = true
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();
            return user;
        }

        public async Task<AuthResponseDTO> LoginAsync(LoginDTO loginDto)
        {
            var user = await _context.Users
                .IgnoreQueryFilters()
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.Email == loginDto.Email);

            if (user == null || user.IsDeleted)
            {
                await _auditService.WriteAsync(
                    "AUTH-LOGIN-FAILURE",
                    email: loginDto.Email,
                    reason: "UserNotFoundOrDeleted");
                throw new AuthFailureException("UserNotFoundOrDeleted");
            }

            var now = DateTime.UtcNow;
            if (user.LockoutEndUtc.HasValue)
            {
                if (user.LockoutEndUtc.Value > now)
                {
                    await _auditService.WriteAsync(
                        "AUTH-LOCKOUT-BLOCKED",
                        userId: user.Id,
                        companyId: user.CompanyId,
                        email: user.Email,
                        reason: "LockoutActive",
                        failedAttempts: user.AccessFailedCount,
                        lockoutEndUtc: user.LockoutEndUtc);
                    throw new AuthFailureException("LockoutActive");
                }

                user.AccessFailedCount = 0;
                user.LockoutEndUtc = null;
                await _context.SaveChangesAsync();
            }

            if (user.Status == "Blocked")
            {
                await _auditService.WriteAsync(
                    "AUTH-LOGIN-FAILURE",
                    userId: user.Id,
                    companyId: user.CompanyId,
                    email: user.Email,
                    reason: "UserBlocked");
                throw new AuthFailureException("UserBlocked");
            }

            if (!user.IsActive)
            {
                await _auditService.WriteAsync(
                    "AUTH-LOGIN-FAILURE",
                    userId: user.Id,
                    companyId: user.CompanyId,
                    email: user.Email,
                    reason: "UserDeactivated");
                throw new AuthFailureException("UserDeactivated");
            }

            if (!_legacyPasswordService.TryVerify(loginDto.Password, user.PasswordHash, user.PasswordSalt, out var passwordMatches))
            {
                _logger.LogWarning("Password data is corrupted for user {UserId}.", user.Id);
                await _auditService.WriteAsync(
                    "AUTH-LOGIN-FAILURE",
                    userId: user.Id,
                    companyId: user.CompanyId,
                    email: user.Email,
                    reason: "PasswordDataCorrupted");
                throw new AuthFailureException("PasswordDataCorrupted");
            }

            if (!passwordMatches)
            {
                user.AccessFailedCount++;

                if (user.AccessFailedCount >= GetMaxFailedAccessAttempts())
                {
                    user.LockoutEndUtc = now.Add(GetLockoutDuration());
                }

                await _context.SaveChangesAsync();
                await _auditService.WriteAsync(
                    "AUTH-LOGIN-FAILURE",
                    userId: user.Id,
                    companyId: user.CompanyId,
                    email: user.Email,
                    reason: "InvalidPassword",
                    failedAttempts: user.AccessFailedCount,
                    lockoutEndUtc: user.LockoutEndUtc);

                if (user.LockoutEndUtc.HasValue)
                {
                    await _auditService.WriteAsync(
                        "AUTH-LOCKOUT-APPLIED",
                        userId: user.Id,
                        companyId: user.CompanyId,
                        email: user.Email,
                        reason: "MaxFailedAttemptsExceeded",
                        failedAttempts: user.AccessFailedCount,
                        lockoutEndUtc: user.LockoutEndUtc);
                }

                throw new AuthFailureException("InvalidPassword");
            }

            if (user.AccessFailedCount > 0 || user.LockoutEndUtc.HasValue)
            {
                user.AccessFailedCount = 0;
                user.LockoutEndUtc = null;
                await _context.SaveChangesAsync();
            }

            if (user.Role == null)
            {
                await _auditService.WriteAsync(
                    "AUTH-LOGIN-FAILURE",
                    userId: user.Id,
                    companyId: user.CompanyId,
                    email: user.Email,
                    reason: "RoleMissing");
                throw new AuthFailureException("RoleMissing");
            }

            var company = await _context.Companies.IgnoreQueryFilters().FirstOrDefaultAsync(c => c.Id == user.CompanyId);
            if (company == null)
            {
                await _auditService.WriteAsync(
                    "AUTH-LOGIN-FAILURE",
                    userId: user.Id,
                    companyId: user.CompanyId,
                    email: user.Email,
                    reason: "CompanyNotFound");
                throw new AuthFailureException("CompanyNotFound");
            }

            if (user.Role.Name != "SuperAdmin")
            {
                if (company.Status == "Blocked")
                {
                    await _auditService.WriteAsync(
                        "AUTH-LOGIN-FAILURE",
                        userId: user.Id,
                        companyId: company.Id,
                        email: user.Email,
                        reason: "CompanyBlocked");
                    throw new AuthFailureException("CompanyBlocked");
                }

                if (company.Status == "Suspended" || !company.IsActive)
                {
                    await _auditService.WriteAsync(
                        "AUTH-LOGIN-FAILURE",
                        userId: user.Id,
                        companyId: company.Id,
                        email: user.Email,
                        reason: "CompanySuspended");
                    throw new AuthFailureException("CompanySuspended");
                }
            }

            var tokenResult = _authTokenFactory.Create(CreateTokenContext(user, company));
            await _auditService.WriteAsync(
                "AUTH-LOGIN-SUCCESS",
                userId: user.Id,
                companyId: company.Id,
                email: user.Email,
                reason: user.Role.Name);

            return new AuthResponseDTO
            {
                Token = tokenResult.Token,
                Email = user.Email,
                Role = user.Role.Name,
                CompanyId = company.Id,
                CompanyName = company.Name,
                ExpiresAt = tokenResult.ExpiresAt
            };
        }

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

        private int GetMaxFailedAccessAttempts()
        {
            var configuredValue = _configuration.GetValue<int?>("AuthSecurity:Lockout:MaxFailedAccessAttempts");
            return configuredValue is > 0 ? configuredValue.Value : DefaultMaxFailedAccessAttempts;
        }

        private TimeSpan GetLockoutDuration()
        {
            var configuredMinutes = _configuration.GetValue<int?>("AuthSecurity:Lockout:LockoutMinutes");
            var minutes = configuredMinutes is > 0 ? configuredMinutes.Value : DefaultLockoutMinutes;
            return TimeSpan.FromMinutes(minutes);
        }

        private static AuthTokenContext CreateTokenContext(User user, Company company) =>
            new(
                user.Email,
                user.Role.Name,
                user.Id,
                user.FullName ?? user.Email,
                company.Id,
                company.Name);
    }
}
