using AccountingSystem.API.Identity;

namespace AccountingSystem.API.Security
{
    public sealed record LoginChallengeTokenContext(
        int IdentityUserId,
        int LegacyUserId);

    public sealed record LoginChallengeTokenPayload(
        int IdentityUserId,
        int LegacyUserId,
        string Purpose);

    public sealed record MfaLoginVerificationResult(
        ApplicationUser IdentityUser,
        bool UsedRecoveryCode);
}
