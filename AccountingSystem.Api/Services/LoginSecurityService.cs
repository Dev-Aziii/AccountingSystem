using AccountingSystem.API.Configuration;
using AccountingSystem.API.Data;
using AccountingSystem.API.Identity;
using AccountingSystem.API.Models;
using AccountingSystem.API.Security;
using AccountingSystem.API.Services.Interfaces;
using AccountingSystem.Shared.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AccountingSystem.API.Services
{
    public class LoginSecurityService : ILoginSecurityService
    {
        private readonly AccountingDbContext _context;
        private readonly IdentityAuthDbContext _identityContext;
        private readonly IAuthSecurityAuditService _auditService;
        private readonly TimeProvider _timeProvider;
        private readonly LoginAttemptPolicyOptions _options;
        private readonly ILogger<LoginSecurityService> _logger;

        public LoginSecurityService(
            AccountingDbContext context,
            IdentityAuthDbContext identityContext,
            IAuthSecurityAuditService auditService,
            IOptions<LoginAttemptPolicyOptions> options,
            TimeProvider timeProvider,
            ILogger<LoginSecurityService> logger)
        {
            _context = context;
            _identityContext = identityContext;
            _auditService = auditService;
            _timeProvider = timeProvider;
            _options = options.Value;
            _logger = logger;
        }

        public async Task EnsureLoginAllowedAsync(User user, ApplicationUser? identityUser, string attemptedEmail)
        {
            identityUser = await ResolveIdentityUserAsync(user, identityUser);
            var state = await _context.UserLoginSecurityStates.FirstOrDefaultAsync(s => s.UserId == user.Id);
            var now = UtcNow;

            if (string.Equals(user.Status, ApplicationUserStatuses.Blocked, StringComparison.Ordinal))
            {
                var disabledReason = state?.DisabledReason ?? ApplicationUserDisableReasons.AdminDisabled;
                await _auditService.WriteAsync(
                    "AUTH-LOGIN-BLOCKED-DISABLED",
                    userId: user.Id,
                    companyId: user.CompanyId,
                    email: user.Email ?? attemptedEmail,
                    reason: disabledReason,
                    disabledReason: disabledReason);
                throw AuthFailureException.CreateAccountDisabled("AccountBlocked");
            }

            await SynchronizeCurrentWindowStateAsync(user, identityUser, now);

            var activeLockoutEndUtc = GetCurrentLockoutEndUtc(user, identityUser);
            if (activeLockoutEndUtc.HasValue && activeLockoutEndUtc.Value > now)
            {
                var remainingSeconds = GetRemainingSeconds(activeLockoutEndUtc.Value, now);
                await _auditService.WriteAsync(
                    "AUTH-LOCKOUT-BLOCKED",
                    userId: user.Id,
                    companyId: user.CompanyId,
                    email: user.Email,
                    reason: "LockoutActive",
                    failedAttempts: user.AccessFailedCount,
                    lockoutEndUtc: activeLockoutEndUtc.Value);
                throw AuthFailureException.CreateTemporaryLockout(activeLockoutEndUtc.Value, remainingSeconds);
            }
        }

        public async Task RecordInvalidPasswordAttemptAsync(User user, ApplicationUser? identityUser)
        {
            identityUser = await ResolveIdentityUserAsync(user, identityUser);

            var now = UtcNow;
            var attemptWindowStart = now.AddMinutes(-_options.AttemptWindowMinutes);
            var failureAttempts = await _context.UserFailedLoginAttempts
                .Where(a => a.UserId == user.Id)
                .ToListAsync();

            var expiredAttempts = failureAttempts.Where(a => a.OccurredAtUtc < attemptWindowStart).ToList();
            if (expiredAttempts.Count > 0)
            {
                _context.UserFailedLoginAttempts.RemoveRange(expiredAttempts);
            }

            var recentFailureCount = failureAttempts.Count - expiredAttempts.Count + 1;
            _context.UserFailedLoginAttempts.Add(new UserFailedLoginAttempt
            {
                UserId = user.Id,
                OccurredAtUtc = now
            });

            user.AccessFailedCount = recentFailureCount;
            if (identityUser != null)
            {
                identityUser.AccessFailedCount = recentFailureCount;
                identityUser.UpdatedAt = now;
            }

            if (recentFailureCount < _options.MaxFailedAccessAttempts)
            {
                await SaveSecurityChangesAsync(identityUser);
                await _auditService.WriteAsync(
                    "AUTH-LOGIN-FAILURE",
                    userId: user.Id,
                    companyId: user.CompanyId,
                    email: user.Email,
                    reason: "InvalidPassword",
                    failedAttempts: recentFailureCount);
                throw new AuthFailureException("InvalidPassword");
            }

            var state = await GetOrCreateStateAsync(user.Id);
            state.ConsecutiveLockoutCount += 1;

            var lockoutDurationMinutes = GetLockoutDurationMinutes(state.ConsecutiveLockoutCount);
            var lockoutEndUtc = now.AddMinutes(lockoutDurationMinutes);

            user.LockoutEndUtc = lockoutEndUtc;
            if (identityUser != null)
            {
                identityUser.LockoutEnd = new DateTimeOffset(lockoutEndUtc, TimeSpan.Zero);
                identityUser.UpdatedAt = now;
            }

            _context.UserLockoutEvents.Add(new UserLockoutEvent
            {
                UserId = user.Id,
                OccurredAtUtc = now,
                LockoutDurationMinutes = lockoutDurationMinutes,
                ConsecutiveLockoutCount = state.ConsecutiveLockoutCount
            });

            var disableWindowStart = now.AddHours(-_options.DisableWindowHours);
            var priorLockoutsInWindow = await _context.UserLockoutEvents.CountAsync(e =>
                e.UserId == user.Id &&
                e.OccurredAtUtc >= disableWindowStart);
            var shouldAutoDisable = priorLockoutsInWindow + 1 >= _options.DisableAfterLockoutEvents;

            if (shouldAutoDisable)
            {
                user.Status = ApplicationUserStatuses.Blocked;
                user.IsActive = false;
                state.DisabledAtUtc = now;
                state.DisabledReason = ApplicationUserDisableReasons.RepeatedLockouts;

                if (identityUser != null)
                {
                    identityUser.Status = ApplicationUserStatuses.Blocked;
                    identityUser.IsActive = false;
                    identityUser.UpdatedAt = now;
                }
            }

            await SaveSecurityChangesAsync(identityUser);

            await _auditService.WriteAsync(
                "AUTH-LOGIN-FAILURE",
                userId: user.Id,
                companyId: user.CompanyId,
                email: user.Email,
                reason: "InvalidPassword",
                failedAttempts: recentFailureCount,
                lockoutEndUtc: lockoutEndUtc);

            await _auditService.WriteAsync(
                "AUTH-LOCKOUT-LEVEL-SELECTED",
                userId: user.Id,
                companyId: user.CompanyId,
                email: user.Email,
                reason: $"ConsecutiveLockout:{state.ConsecutiveLockoutCount}",
                failedAttempts: recentFailureCount,
                lockoutEndUtc: lockoutEndUtc,
                lockoutDurationMinutes: lockoutDurationMinutes);

            await _auditService.WriteAsync(
                "AUTH-LOCKOUT-APPLIED",
                userId: user.Id,
                companyId: user.CompanyId,
                email: user.Email,
                reason: "MaxFailedAttemptsExceeded",
                failedAttempts: recentFailureCount,
                lockoutEndUtc: lockoutEndUtc,
                lockoutDurationMinutes: lockoutDurationMinutes);

            if (shouldAutoDisable)
            {
                await _auditService.WriteAsync(
                    "AUTH-ACCOUNT-AUTO-DISABLED",
                    userId: user.Id,
                    companyId: user.CompanyId,
                    email: user.Email,
                    reason: ApplicationUserDisableReasons.RepeatedLockouts,
                    failedAttempts: recentFailureCount,
                    lockoutEndUtc: lockoutEndUtc,
                    lockoutDurationMinutes: lockoutDurationMinutes,
                    disabledReason: ApplicationUserDisableReasons.RepeatedLockouts);
                throw AuthFailureException.CreateAccountDisabled("AccountAutoDisabled");
            }

            throw AuthFailureException.CreateTemporaryLockout(lockoutEndUtc, GetRemainingSeconds(lockoutEndUtc, now));
        }

        public async Task ResetAfterSuccessfulLoginAsync(User user, ApplicationUser? identityUser)
        {
            identityUser = await ResolveIdentityUserAsync(user, identityUser);

            var state = await GetOrCreateStateAsync(user.Id);
            var now = UtcNow;

            var failureAttempts = await _context.UserFailedLoginAttempts
                .Where(a => a.UserId == user.Id)
                .ToListAsync();
            if (failureAttempts.Count > 0)
            {
                _context.UserFailedLoginAttempts.RemoveRange(failureAttempts);
            }

            state.ConsecutiveLockoutCount = 0;
            state.LastSuccessfulLoginAtUtc = now;

            user.AccessFailedCount = 0;
            user.LockoutEndUtc = null;

            if (identityUser != null)
            {
                identityUser.AccessFailedCount = 0;
                identityUser.LockoutEnd = null;
                identityUser.UpdatedAt = now;
            }

            await SaveSecurityChangesAsync(identityUser);
        }

        public async Task HandleAdminStatusChangeAsync(User user, string previousStatus, string newStatus)
        {
            var state = await GetOrCreateStateAsync(user.Id);
            var now = UtcNow;

            if (!string.Equals(previousStatus, ApplicationUserStatuses.Blocked, StringComparison.Ordinal) &&
                string.Equals(newStatus, ApplicationUserStatuses.Blocked, StringComparison.Ordinal))
            {
                state.DisabledAtUtc = now;
                state.DisabledReason = ApplicationUserDisableReasons.AdminDisabled;
                await _context.SaveChangesAsync();

                await _auditService.WriteAsync(
                    "AUTH-ACCOUNT-DISABLED-ADMIN",
                    userId: user.Id,
                    companyId: user.CompanyId,
                    email: user.Email,
                    reason: ApplicationUserDisableReasons.AdminDisabled,
                    disabledReason: ApplicationUserDisableReasons.AdminDisabled);
                return;
            }

            if (string.Equals(previousStatus, ApplicationUserStatuses.Blocked, StringComparison.Ordinal) &&
                !string.Equals(newStatus, ApplicationUserStatuses.Blocked, StringComparison.Ordinal))
            {
                var identityUser = await ResolveIdentityUserAsync(user, null);
                var failureAttempts = await _context.UserFailedLoginAttempts
                    .Where(a => a.UserId == user.Id)
                    .ToListAsync();
                if (failureAttempts.Count > 0)
                {
                    _context.UserFailedLoginAttempts.RemoveRange(failureAttempts);
                }

                state.DisabledAtUtc = null;
                state.DisabledReason = null;
                state.ConsecutiveLockoutCount = 0;

                user.AccessFailedCount = 0;
                user.LockoutEndUtc = null;

                if (identityUser != null)
                {
                    identityUser.AccessFailedCount = 0;
                    identityUser.LockoutEnd = null;
                    identityUser.UpdatedAt = now;
                    await _identityContext.SaveChangesAsync();
                }

                await _context.SaveChangesAsync();

                await _auditService.WriteAsync(
                    "AUTH-ACCOUNT-ENABLED-ADMIN",
                    userId: user.Id,
                    companyId: user.CompanyId,
                    email: user.Email,
                    reason: "StatusChangedFromBlocked");
            }
        }

        private async Task SynchronizeCurrentWindowStateAsync(User user, ApplicationUser? identityUser, DateTime now)
        {
            var attemptWindowStart = now.AddMinutes(-_options.AttemptWindowMinutes);
            var failureAttempts = await _context.UserFailedLoginAttempts
                .Where(a => a.UserId == user.Id)
                .ToListAsync();

            var expiredAttempts = failureAttempts.Where(a => a.OccurredAtUtc < attemptWindowStart).ToList();
            if (expiredAttempts.Count > 0)
            {
                _context.UserFailedLoginAttempts.RemoveRange(expiredAttempts);
            }

            var currentFailureCount = failureAttempts.Count - expiredAttempts.Count;
            var currentLockoutEndUtc = GetCurrentLockoutEndUtc(user, identityUser);
            var hasExpiredLockout = currentLockoutEndUtc.HasValue && currentLockoutEndUtc.Value <= now;

            var requiresSave = false;

            if (user.AccessFailedCount != currentFailureCount)
            {
                user.AccessFailedCount = currentFailureCount;
                requiresSave = true;
            }

            if (hasExpiredLockout && user.LockoutEndUtc.HasValue)
            {
                user.LockoutEndUtc = null;
                requiresSave = true;
            }

            if (identityUser != null)
            {
                if (identityUser.AccessFailedCount != currentFailureCount)
                {
                    identityUser.AccessFailedCount = currentFailureCount;
                    identityUser.UpdatedAt = now;
                }

                if (hasExpiredLockout && identityUser.LockoutEnd.HasValue)
                {
                    identityUser.LockoutEnd = null;
                    identityUser.UpdatedAt = now;
                }
            }

            if (!requiresSave && identityUser == null)
            {
                return;
            }

            await SaveSecurityChangesAsync(identityUser);
        }

        private async Task<UserLoginSecurityState> GetOrCreateStateAsync(int userId)
        {
            var state = await _context.UserLoginSecurityStates.FirstOrDefaultAsync(s => s.UserId == userId);
            if (state != null)
            {
                return state;
            }

            state = new UserLoginSecurityState
            {
                UserId = userId
            };
            _context.UserLoginSecurityStates.Add(state);
            return state;
        }

        private async Task<ApplicationUser?> ResolveIdentityUserAsync(User user, ApplicationUser? identityUser)
        {
            if (identityUser != null)
            {
                return identityUser;
            }

            return await _identityContext.Users
                .FirstOrDefaultAsync(u => u.LegacyUserId == user.Id);
        }

        private async Task SaveSecurityChangesAsync(ApplicationUser? identityUser)
        {
            await _context.SaveChangesAsync();

            if (identityUser != null)
            {
                await _identityContext.SaveChangesAsync();
            }
        }

        private static DateTime? GetCurrentLockoutEndUtc(User user, ApplicationUser? identityUser)
        {
            var identityLockoutEndUtc = identityUser?.LockoutEnd?.UtcDateTime;
            return new[] { user.LockoutEndUtc, identityLockoutEndUtc }
                .Where(value => value.HasValue)
                .Select(value => value!.Value)
                .DefaultIfEmpty()
                .Max();
        }

        private int GetLockoutDurationMinutes(int consecutiveLockoutCount) => consecutiveLockoutCount switch
        {
            <= 1 => _options.FirstLockoutMinutes,
            2 => _options.SecondLockoutMinutes,
            _ => _options.SubsequentLockoutMinutes
        };

        private static int GetRemainingSeconds(DateTime lockoutEndUtc, DateTime now) =>
            Math.Max(1, (int)Math.Ceiling((lockoutEndUtc - now).TotalSeconds));

        private DateTime UtcNow => _timeProvider.GetUtcNow().UtcDateTime;
    }
}
