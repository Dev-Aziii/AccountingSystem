using AccountingSystem.API.Configuration;
using AccountingSystem.API.Data;
using AccountingSystem.API.Identity;
using AccountingSystem.API.Models;
using AccountingSystem.API.Security;
using AccountingSystem.API.Services.Interfaces;
using AccountingSystem.Shared.DTOs;
using AccountingSystem.Shared.Validation;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Transactions;

namespace AccountingSystem.API.Services
{
    public class AuthService : IAuthService
    {
        private const int DefaultMaxFailedAccessAttempts = 5;
        private const int DefaultLockoutMinutes = 15;

        private readonly AccountingDbContext _context;
        private readonly IdentityAuthDbContext _identityContext;
        private readonly IConfiguration _configuration;
        private readonly ICaptchaService _captchaService;
        private readonly ILogger<AuthService> _logger;
        private readonly IAuthSecurityAuditService _auditService;
        private readonly ILegacyPasswordService _legacyPasswordService;
        private readonly IAuthTokenFactory _authTokenFactory;
        private readonly IIdentityAccountService _identityAccountService;
        private readonly IAccountEmailService _accountEmailService;
        private readonly UserManager<ApplicationUser> _userManager;

        public AuthService(
            AccountingDbContext context,
            IdentityAuthDbContext identityContext,
            IConfiguration configuration,
            ICaptchaService captchaService,
            ILogger<AuthService> logger,
            IAuthSecurityAuditService auditService,
            ILegacyPasswordService legacyPasswordService,
            IAuthTokenFactory authTokenFactory,
            IIdentityAccountService identityAccountService,
            IAccountEmailService accountEmailService,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _identityContext = identityContext;
            _configuration = configuration;
            _captchaService = captchaService;
            _logger = logger;
            _auditService = auditService;
            _legacyPasswordService = legacyPasswordService;
            _authTokenFactory = authTokenFactory;
            _identityAccountService = identityAccountService;
            _accountEmailService = accountEmailService;
            _userManager = userManager;
        }

        public async Task<CurrentProfileDTO> GetCurrentProfileAsync(int userId)
        {
            var user = await _context.Users
                .IgnoreQueryFilters()
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
            {
                throw new Exception("User not found.");
            }

            var companyName = await _context.Companies
                .IgnoreQueryFilters()
                .Where(c => c.Id == user.CompanyId)
                .Select(c => c.Name)
                .FirstOrDefaultAsync()
                ?? string.Empty;

            return new CurrentProfileDTO
            {
                Email = user.Email,
                FullName = user.FullName,
                Role = user.Role?.Name ?? string.Empty,
                CompanyId = user.CompanyId,
                CompanyName = companyName
            };
        }

        public async Task UpdateProfileAsync(int userId, UpdateProfileDTO dto)
        {
            var user = await _context.Users
                .IgnoreQueryFilters()
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
            {
                await _auditService.WriteAsync("AUTH-PROFILE-UPDATE-FAILURE", userId: userId, reason: "UserNotFound");
                throw new Exception("User not found.");
            }

            var emailChanged = !string.Equals(user.Email, dto.Email, StringComparison.OrdinalIgnoreCase);
            if (await EmailExistsForDifferentUserAsync(dto.Email, user.Id))
            {
                await _auditService.WriteAsync(
                    "AUTH-PROFILE-UPDATE-FAILURE",
                    userId: user.Id,
                    companyId: user.CompanyId,
                    email: user.Email,
                    reason: "EmailAlreadyInUse");
                throw new Exception("Email is already in use.");
            }

            using var transaction = CreateTransactionScope();

            var identityUser = await ResolveIdentityUserAsync(user);
            if (identityUser != null)
            {
                identityUser.Email = dto.Email;
                identityUser.UserName = dto.Email;
                identityUser.NormalizedEmail = _userManager.NormalizeEmail(dto.Email);
                identityUser.NormalizedUserName = _userManager.NormalizeName(dto.Email);
                identityUser.FullName = dto.FullName;
                identityUser.UpdatedAt = DateTime.UtcNow;

                var identityResult = await _userManager.UpdateAsync(identityUser);
                EnsureIdentitySucceeded(identityResult, "UpdateProfile");
            }

            user.FullName = dto.FullName;
            user.Email = dto.Email;

            await _context.SaveChangesAsync();
            transaction.Complete();

            await _auditService.WriteAsync(
                "AUTH-PROFILE-UPDATE",
                userId: user.Id,
                companyId: user.CompanyId,
                email: user.Email,
                reason: emailChanged ? "EmailChanged" : "ProfileUpdated");
        }

        public async Task ChangePasswordAsync(int userId, ChangePasswordDTO dto)
        {
            var user = await _context.Users
                .IgnoreQueryFilters()
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
            {
                await _auditService.WriteAsync("AUTH-PASSWORD-CHANGE-FAILURE", userId: userId, reason: "UserNotFound");
                throw new Exception("User not found.");
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

            using var transaction = CreateTransactionScope();

            var identityUser = await ResolveIdentityUserAsync(user);
            if (identityUser == null)
            {
                await _identityAccountService.EnsureProvisionedAsync(CreateIdentitySnapshot(user, user.Role.Name), dto.CurrentPassword);
                identityUser = await RequireIdentityUserAsync(user);
            }
            else if (!HasUsableIdentityPassword(identityUser))
            {
                if (!TryVerifyLegacyPassword(dto.CurrentPassword, user, out var legacyPasswordMatches))
                {
                    await _auditService.WriteAsync(
                        "AUTH-PASSWORD-CHANGE-FAILURE",
                        userId: user.Id,
                        companyId: user.CompanyId,
                        email: user.Email,
                        reason: "PasswordDataCorrupted");
                    throw new Exception("Password reset is required before this account can change its password.");
                }

                if (!legacyPasswordMatches)
                {
                    await _auditService.WriteAsync(
                        "AUTH-PASSWORD-CHANGE-FAILURE",
                        userId: user.Id,
                        companyId: user.CompanyId,
                        email: user.Email,
                        reason: "InvalidCurrentPassword");
                    throw new Exception("Incorrect current password.");
                }

                identityUser.PasswordHash = _userManager.PasswordHasher.HashPassword(identityUser, dto.CurrentPassword);
                identityUser.SecurityStamp = Guid.NewGuid().ToString("N");
                identityUser.UpdatedAt = DateTime.UtcNow;

                var bootstrapResult = await _userManager.UpdateAsync(identityUser);
                EnsureIdentitySucceeded(bootstrapResult, "BootstrapIdentityPasswordForChange");
            }

            var changePasswordResult = await _userManager.ChangePasswordAsync(identityUser, dto.CurrentPassword, dto.NewPassword);
            if (!changePasswordResult.Succeeded)
            {
                await _auditService.WriteAsync(
                    "AUTH-PASSWORD-CHANGE-FAILURE",
                    userId: user.Id,
                    companyId: user.CompanyId,
                    email: user.Email,
                    reason: changePasswordResult.Errors.FirstOrDefault()?.Code ?? "IdentityPasswordChangeFailed");

                if (changePasswordResult.Errors.Any(e => string.Equals(e.Code, "PasswordMismatch", StringComparison.OrdinalIgnoreCase)))
                {
                    throw new Exception("Incorrect current password.");
                }

                throw new Exception(GetIdentityErrorMessage(changePasswordResult, "Unable to change password. Please try again."));
            }

            await ResetIdentityLockoutAsync(identityUser);
            var refreshedIdentityUser = await RequireIdentityUserAsync(user);
            ApplyIdentitySecurityMirror(user, refreshedIdentityUser);
            ClearLegacyPassword(user);
            await _context.SaveChangesAsync();

            transaction.Complete();

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

            if (await EmailExistsForDifferentUserAsync(dto.AdminEmail, null))
            {
                await _auditService.WriteAsync(
                    "AUTH-REGISTER-COMPANY-FAILURE",
                    email: dto.AdminEmail,
                    reason: "EmailAlreadyExists");
                throw new Exception("Email already exists.");
            }

            Company? company = null;
            User? user = null;

            using var transaction = CreateTransactionScope();

            try
            {
                company = new Company
                {
                    Name = dto.CompanyName,
                    CreatedAt = DateTime.UtcNow,
                    IsActive = true,
                    Status = "Active",
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
                    throw new Exception("System role 'Admin' is missing.");
                }

                user = new User
                {
                    CompanyId = company.Id,
                    Email = dto.AdminEmail,
                    FullName = dto.AdminFullName,
                    RoleId = adminRole.Id,
                    Role = adminRole,
                    PasswordHash = string.Empty,
                    PasswordSalt = null,
                    IsActive = true,
                    Status = "Active"
                };

                _context.Users.Add(user);
                await _context.SaveChangesAsync();

                await _identityAccountService.EnsureProvisionedAsync(
                    CreateIdentitySnapshot(user, adminRole.Name),
                    dto.Password);

                await SeedCompanyDataAsync(company.Id);
                transaction.Complete();
            }
            catch (DbUpdateException ex)
            {
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

            var tokenResult = _authTokenFactory.Create(CreateTokenContext(user!, company!));
            await _auditService.WriteAsync(
                "AUTH-REGISTER-COMPANY",
                userId: user!.Id,
                companyId: company!.Id,
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

        public async Task<User> RegisterAsync(RegisterDTO registerDto)
        {
            if (!PasswordPolicy.TryValidate(registerDto.Password, out var passwordValidationMessage))
            {
                throw new Exception(passwordValidationMessage);
            }

            if (await EmailExistsForDifferentUserAsync(registerDto.Email, null))
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

            using var transaction = CreateTransactionScope();

            var user = new User
            {
                Email = registerDto.Email,
                FullName = registerDto.FullName,
                RoleId = role.Id,
                PasswordHash = string.Empty,
                PasswordSalt = null,
                IsActive = true,
                Status = "Active"
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            await _identityAccountService.EnsureProvisionedAsync(
                CreateIdentitySnapshot(user, role.Name),
                registerDto.Password);

            transaction.Complete();
            return user;
        }

        public async Task<AuthResponseDTO> LoginAsync(LoginDTO loginDto)
        {
            var normalizedEmail = _userManager.NormalizeEmail(loginDto.Email);
            var identityUser = string.IsNullOrWhiteSpace(normalizedEmail)
                ? null
                : await _identityContext.Users.FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail);

            var user = await ResolveLegacyUserAsync(loginDto.Email, identityUser);
            if (user == null || user.IsDeleted)
            {
                await _auditService.WriteAsync(
                    "AUTH-LOGIN-FAILURE",
                    email: loginDto.Email,
                    reason: "UserNotFoundOrDeleted");
                throw new AuthFailureException("UserNotFoundOrDeleted");
            }

            if (user.Role == null)
            {
                user.Role = await _context.Roles.FirstOrDefaultAsync(r => r.Id == user.RoleId)
                    ?? throw new AuthFailureException("RoleMissing");
            }

            if (identityUser == null && user.Id > 0)
            {
                identityUser = await _identityAccountService.FindByLegacyUserIdAsync(user.Id);
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

            if (HasUsableIdentityPassword(identityUser))
            {
                await ValidateIdentityPasswordAsync(identityUser!, user, loginDto.Password);
            }
            else
            {
                await ValidateLegacyPasswordFallbackAsync(user, loginDto.Password);
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

        public async Task SendPasswordResetAsync(ForgotPasswordDTO dto)
        {
            ApplicationUser? identityUser = null;
            User? legacyUser = null;

            try
            {
                identityUser = await _identityAccountService.FindByEmailAsync(dto.Email);
                legacyUser = await _context.Users
                    .IgnoreQueryFilters()
                    .Include(u => u.Role)
                    .FirstOrDefaultAsync(u => u.Email == dto.Email && !u.IsDeleted);

                if (identityUser == null && legacyUser != null && legacyUser.Role != null)
                {
                    using var transaction = CreateTransactionScope();
                    await _identityAccountService.EnsureProvisionedWithoutPasswordAsync(CreateIdentitySnapshot(legacyUser, legacyUser.Role.Name));
                    transaction.Complete();

                    identityUser = await RequireIdentityUserAsync(legacyUser);
                }

                if (identityUser == null)
                {
                    await _auditService.WriteAsync("AUTH-FORGOT-PASSWORD", email: dto.Email, reason: "NoMatchingAccount");
                    return;
                }

                var token = await _userManager.GeneratePasswordResetTokenAsync(identityUser);
                var encodedToken = EncodeResetToken(token);
                var resetLink = BuildPasswordResetLink(identityUser.Email!, encodedToken);
                await _accountEmailService.SendPasswordResetAsync(
                    identityUser.Email!,
                    identityUser.FullName,
                    resetLink);

                await _auditService.WriteAsync(
                    "AUTH-FORGOT-PASSWORD",
                    userId: identityUser.LegacyUserId,
                    companyId: identityUser.CompanyId,
                    email: identityUser.Email,
                    reason: "ResetEmailSent");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process forgot-password request for {Email}.", dto.Email);
                await _auditService.WriteAsync(
                    "AUTH-FORGOT-PASSWORD-FAILURE",
                    userId: identityUser?.LegacyUserId,
                    companyId: identityUser?.CompanyId ?? legacyUser?.CompanyId,
                    email: dto.Email,
                    reason: ex.GetType().Name);
            }
        }

        public async Task ResetPasswordAsync(ResetPasswordDTO dto)
        {
            if (!PasswordPolicy.TryValidate(dto.NewPassword, out var passwordValidationMessage))
            {
                throw new Exception(passwordValidationMessage);
            }

            var identityUser = await _identityAccountService.FindByEmailAsync(dto.Email);
            if (identityUser == null)
            {
                throw new Exception("The password reset request is invalid or has expired.");
            }

            var decodedToken = DecodeResetToken(dto.Token);
            using var transaction = CreateTransactionScope();

            var resetResult = await _userManager.ResetPasswordAsync(identityUser, decodedToken, dto.NewPassword);
            if (!resetResult.Succeeded)
            {
                await _auditService.WriteAsync(
                    "AUTH-RESET-PASSWORD-FAILURE",
                    userId: identityUser.LegacyUserId,
                    companyId: identityUser.CompanyId,
                    email: identityUser.Email,
                    reason: resetResult.Errors.FirstOrDefault()?.Code ?? "ResetPasswordFailed");
                throw new Exception("The password reset request is invalid or has expired.");
            }

            await ResetIdentityLockoutAsync(identityUser);

            var legacyUser = await ResolveLegacyUserAsync(dto.Email, identityUser);
            if (legacyUser != null)
            {
                ApplyIdentitySecurityMirror(legacyUser, await RequireIdentityUserByIdAsync(identityUser.Id));
                ClearLegacyPassword(legacyUser);
                await _context.SaveChangesAsync();
            }

            transaction.Complete();

            await _auditService.WriteAsync(
                "AUTH-RESET-PASSWORD",
                userId: identityUser.LegacyUserId,
                companyId: identityUser.CompanyId,
                email: identityUser.Email,
                reason: "PasswordReset");
        }

        private async Task ValidateIdentityPasswordAsync(ApplicationUser identityUser, User legacyUser, string password)
        {
            if (await _userManager.IsLockedOutAsync(identityUser))
            {
                var lockedUser = await RequireIdentityUserByIdAsync(identityUser.Id);
                ApplyIdentitySecurityMirror(legacyUser, lockedUser);
                await _context.SaveChangesAsync();

                await _auditService.WriteAsync(
                    "AUTH-LOCKOUT-BLOCKED",
                    userId: legacyUser.Id,
                    companyId: legacyUser.CompanyId,
                    email: legacyUser.Email,
                    reason: "IdentityLockoutActive",
                    failedAttempts: lockedUser.AccessFailedCount,
                    lockoutEndUtc: lockedUser.LockoutEnd?.UtcDateTime);
                throw new AuthFailureException("LockoutActive");
            }

            var passwordMatches = await _userManager.CheckPasswordAsync(identityUser, password);
            if (!passwordMatches)
            {
                await _userManager.AccessFailedAsync(identityUser);
                var failedUser = await RequireIdentityUserByIdAsync(identityUser.Id);
                ApplyIdentitySecurityMirror(legacyUser, failedUser);
                await _context.SaveChangesAsync();

                await _auditService.WriteAsync(
                    "AUTH-LOGIN-FAILURE",
                    userId: legacyUser.Id,
                    companyId: legacyUser.CompanyId,
                    email: legacyUser.Email,
                    reason: "InvalidPassword",
                    failedAttempts: failedUser.AccessFailedCount,
                    lockoutEndUtc: failedUser.LockoutEnd?.UtcDateTime);

                if (failedUser.LockoutEnd.HasValue && failedUser.LockoutEnd.Value > DateTimeOffset.UtcNow)
                {
                    await _auditService.WriteAsync(
                        "AUTH-LOCKOUT-APPLIED",
                        userId: legacyUser.Id,
                        companyId: legacyUser.CompanyId,
                        email: legacyUser.Email,
                        reason: "MaxFailedAttemptsExceeded",
                        failedAttempts: failedUser.AccessFailedCount,
                        lockoutEndUtc: failedUser.LockoutEnd?.UtcDateTime);
                }

                throw new AuthFailureException("InvalidPassword");
            }

            await ResetIdentityLockoutAsync(identityUser);
            var refreshedUser = await RequireIdentityUserByIdAsync(identityUser.Id);
            ApplyIdentitySecurityMirror(legacyUser, refreshedUser);
            ClearLegacyPassword(legacyUser);
            await _context.SaveChangesAsync();
        }

        private async Task ValidateLegacyPasswordFallbackAsync(User user, string password)
        {
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

            if (!TryVerifyLegacyPassword(password, user, out var passwordMatches))
            {
                _logger.LogWarning("Password data is corrupted for legacy user {UserId}.", user.Id);
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

            using var transaction = CreateTransactionScope();

            await _identityAccountService.EnsureProvisionedAsync(CreateIdentitySnapshot(user, user.Role.Name), password);
            var identityUser = await RequireIdentityUserAsync(user);
            await ResetIdentityLockoutAsync(identityUser);

            var refreshedIdentityUser = await RequireIdentityUserByIdAsync(identityUser.Id);
            ApplyIdentitySecurityMirror(user, refreshedIdentityUser);
            ClearLegacyPassword(user);
            await _context.SaveChangesAsync();

            transaction.Complete();
        }

        private async Task<User?> ResolveLegacyUserAsync(string email, ApplicationUser? identityUser)
        {
            if (identityUser?.LegacyUserId is int legacyUserId)
            {
                var linkedUser = await _context.Users
                    .IgnoreQueryFilters()
                    .Include(u => u.Role)
                    .FirstOrDefaultAsync(u => u.Id == legacyUserId);

                if (linkedUser != null)
                {
                    return linkedUser;
                }
            }

            return await _context.Users
                .IgnoreQueryFilters()
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.Email == email);
        }

        private async Task<ApplicationUser?> ResolveIdentityUserAsync(User legacyUser)
        {
            var linkedUser = await _identityAccountService.FindByLegacyUserIdAsync(legacyUser.Id);
            if (linkedUser != null)
            {
                return linkedUser;
            }

            return await _identityAccountService.FindByEmailAsync(legacyUser.Email);
        }

        private async Task<ApplicationUser> RequireIdentityUserAsync(User legacyUser)
        {
            return await ResolveIdentityUserAsync(legacyUser)
                ?? throw new InvalidOperationException($"Identity user was not found for legacy user {legacyUser.Id}.");
        }

        private async Task<ApplicationUser> RequireIdentityUserByIdAsync(int identityUserId)
        {
            return await _identityContext.Users.FirstOrDefaultAsync(u => u.Id == identityUserId)
                ?? throw new InvalidOperationException($"Identity user {identityUserId} was not found.");
        }

        private async Task<bool> EmailExistsForDifferentUserAsync(string email, int? legacyUserId)
        {
            var legacyEmailExists = await _context.Users
                .IgnoreQueryFilters()
                .AnyAsync(u => u.Email == email && (!legacyUserId.HasValue || u.Id != legacyUserId.Value));

            if (legacyEmailExists)
            {
                return true;
            }

            var identityUser = await _identityAccountService.FindByEmailAsync(email);
            return identityUser != null && (!legacyUserId.HasValue || identityUser.LegacyUserId != legacyUserId.Value);
        }

        private bool TryVerifyLegacyPassword(string password, User user, out bool passwordMatches)
        {
            return _legacyPasswordService.TryVerify(password, user.PasswordHash, user.PasswordSalt, out passwordMatches);
        }

        private async Task ResetIdentityLockoutAsync(ApplicationUser identityUser)
        {
            await _userManager.ResetAccessFailedCountAsync(identityUser);
            await _userManager.SetLockoutEndDateAsync(identityUser, null);
        }

        private static bool HasUsableIdentityPassword(ApplicationUser? identityUser)
        {
            return !string.IsNullOrWhiteSpace(identityUser?.PasswordHash);
        }

        private static void ClearLegacyPassword(User user)
        {
            user.PasswordHash = string.Empty;
            user.PasswordSalt = null;
        }

        private static void ApplyIdentitySecurityMirror(User user, ApplicationUser identityUser)
        {
            user.AccessFailedCount = identityUser.AccessFailedCount;
            user.LockoutEndUtc = identityUser.LockoutEnd?.UtcDateTime;
        }

        private static string GetIdentityErrorMessage(IdentityResult result, string fallbackMessage)
        {
            return result.Errors.Select(e => e.Description).FirstOrDefault()
                ?? fallbackMessage;
        }

        private static void EnsureIdentitySucceeded(IdentityResult result, string operation)
        {
            if (result.Succeeded)
            {
                return;
            }

            var details = string.Join("; ", result.Errors.Select(e => $"{e.Code}:{e.Description}"));
            throw new InvalidOperationException($"Identity operation '{operation}' failed: {details}");
        }

        private string BuildPasswordResetLink(string email, string encodedToken)
        {
            var clientBaseUrl = _configuration["AppUrls:ClientBaseUrl"]!.TrimEnd('/');
            return $"{clientBaseUrl}/reset-password?email={Uri.EscapeDataString(email)}&token={Uri.EscapeDataString(encodedToken)}";
        }

        private static string EncodeResetToken(string token)
        {
            return WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
        }

        private static string DecodeResetToken(string encodedToken)
        {
            try
            {
                return Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(encodedToken));
            }
            catch (FormatException)
            {
                throw new Exception("The password reset request is invalid or has expired.");
            }
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

        private static TransactionScope CreateTransactionScope()
        {
            return new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
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

        private static LegacyIdentityUserSnapshot CreateIdentitySnapshot(User user, string roleName) =>
            new(
                user.Id,
                user.CompanyId,
                user.Email,
                user.FullName ?? user.Email,
                user.Status,
                user.IsActive,
                user.IsDeleted,
                roleName);

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
