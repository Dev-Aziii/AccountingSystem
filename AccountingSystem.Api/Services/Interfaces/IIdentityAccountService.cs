using AccountingSystem.API.Identity;

namespace AccountingSystem.API.Services.Interfaces
{
    public interface IIdentityAccountService
    {
        Task EnsureProvisionedAsync(LegacyIdentityUserSnapshot snapshot, string plainTextPassword, CancellationToken cancellationToken = default);

        Task EnsureProvisionedWithoutPasswordAsync(LegacyIdentityUserSnapshot snapshot, CancellationToken cancellationToken = default);

        Task SyncExistingAsync(LegacyIdentityUserSnapshot snapshot, CancellationToken cancellationToken = default);

        Task SyncPasswordAsync(LegacyIdentityUserSnapshot snapshot, string plainTextPassword, bool createIfMissing, CancellationToken cancellationToken = default);

        Task<ApplicationUser?> FindByEmailAsync(string email, CancellationToken cancellationToken = default);

        Task<ApplicationUser?> FindByLegacyUserIdAsync(int legacyUserId, CancellationToken cancellationToken = default);
    }
}
