using AccountingSystem.API.Identity;

namespace AccountingSystem.API.Services.Interfaces
{
    public interface IIdentityAccountService
    {
        Task EnsureProvisionedAsync(LegacyIdentityUserSnapshot snapshot, string plainTextPassword, CancellationToken cancellationToken = default);

        Task SyncExistingAsync(LegacyIdentityUserSnapshot snapshot, CancellationToken cancellationToken = default);

        Task SyncPasswordAsync(LegacyIdentityUserSnapshot snapshot, string plainTextPassword, bool createIfMissing, CancellationToken cancellationToken = default);
    }
}
