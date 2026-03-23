namespace AccountingSystem.API.Identity
{
    public sealed record LegacyIdentityUserSnapshot(
        int LegacyUserId,
        int CompanyId,
        string Email,
        string FullName,
        string Status,
        bool IsActive,
        bool IsDeleted,
        string RoleName,
        bool? RequireEmailConfirmation = null,
        bool? EmailConfirmed = null);
}
