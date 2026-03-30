using AccountingSystem.API.Identity;

namespace AccountingSystem.API.Services.Interfaces
{
    public interface ILegacyIdentityBridgeService
    {
        Task SyncProvisionedUserAsync(LegacyIdentityUserSnapshot snapshot, string plainTextPassword);

        Task SyncAfterSuccessfulLoginAsync(LegacyIdentityUserSnapshot snapshot, string plainTextPassword);

        Task SyncExistingUserProfileAsync(LegacyIdentityUserSnapshot snapshot);

        Task SyncPasswordChangeAsync(LegacyIdentityUserSnapshot snapshot, string plainTextPassword);

        Task SyncExistingUserStatusAsync(LegacyIdentityUserSnapshot snapshot);
    }
}
