using AccountingSystem.API.Security;
using AccountingSystem.Shared.DTOs;

namespace AccountingSystem.API.Services.Interfaces
{
    public interface IMfaService
    {
        Task<MfaStatusDTO> GetStatusAsync(int legacyUserId);

        Task<MfaSetupDTO> BeginAuthenticatorSetupAsync(int legacyUserId);

        Task<MfaSetupDTO> ResetAuthenticatorAsync(int legacyUserId, MfaReauthenticationDTO dto);

        Task<RecoveryCodesDTO> VerifyAuthenticatorSetupAsync(int legacyUserId, VerifyAuthenticatorSetupDTO dto);

        Task<RecoveryCodesDTO> RegenerateRecoveryCodesAsync(int legacyUserId, MfaReauthenticationDTO dto);

        Task DisableAsync(int legacyUserId, MfaReauthenticationDTO dto);

        Task<MfaLoginVerificationResult> VerifyLoginChallengeAsync(LoginMfaDTO dto);
    }
}
