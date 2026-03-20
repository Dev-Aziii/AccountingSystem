using AccountingSystem.API.Identity;
using AccountingSystem.API.Services.Interfaces;

namespace AccountingSystem.API.Services
{
    public class LegacyIdentityBridgeService : ILegacyIdentityBridgeService
    {
        private readonly IIdentityAccountService _identityAccountService;
        private readonly IAuthSecurityAuditService _auditService;
        private readonly ILogger<LegacyIdentityBridgeService> _logger;

        public LegacyIdentityBridgeService(
            IIdentityAccountService identityAccountService,
            IAuthSecurityAuditService auditService,
            ILogger<LegacyIdentityBridgeService> logger)
        {
            _identityAccountService = identityAccountService;
            _auditService = auditService;
            _logger = logger;
        }

        public Task SyncProvisionedUserAsync(LegacyIdentityUserSnapshot snapshot, string plainTextPassword)
        {
            return ExecuteSafeAsync(
                "Provision",
                snapshot,
                () => _identityAccountService.EnsureProvisionedAsync(snapshot, plainTextPassword));
        }

        public Task SyncAfterSuccessfulLoginAsync(LegacyIdentityUserSnapshot snapshot, string plainTextPassword)
        {
            return ExecuteSafeAsync(
                "SuccessfulLogin",
                snapshot,
                () => _identityAccountService.EnsureProvisionedAsync(snapshot, plainTextPassword));
        }

        public Task SyncExistingUserProfileAsync(LegacyIdentityUserSnapshot snapshot)
        {
            return ExecuteSafeAsync(
                "ProfileUpdate",
                snapshot,
                () => _identityAccountService.SyncExistingAsync(snapshot));
        }

        public Task SyncPasswordChangeAsync(LegacyIdentityUserSnapshot snapshot, string plainTextPassword)
        {
            return ExecuteSafeAsync(
                "PasswordChange",
                snapshot,
                () => _identityAccountService.SyncPasswordAsync(snapshot, plainTextPassword, createIfMissing: true));
        }

        public Task SyncExistingUserStatusAsync(LegacyIdentityUserSnapshot snapshot)
        {
            return ExecuteSafeAsync(
                "StatusChange",
                snapshot,
                () => _identityAccountService.SyncExistingAsync(snapshot));
        }

        private async Task ExecuteSafeAsync(string operation, LegacyIdentityUserSnapshot snapshot, Func<Task> action)
        {
            try
            {
                await action();
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Identity sync failed during {Operation} for legacy user {LegacyUserId} ({Email}).",
                    operation,
                    snapshot.LegacyUserId,
                    snapshot.Email);

                await _auditService.WriteAsync(
                    "IDENTITY-SYNC-FAILURE",
                    userId: snapshot.LegacyUserId,
                    companyId: snapshot.CompanyId,
                    email: snapshot.Email,
                    reason: operation,
                    policy: ex.GetType().Name);
            }
        }
    }
}
