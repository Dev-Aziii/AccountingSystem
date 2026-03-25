using AccountingSystem.API.Models;
using AccountingSystem.Shared.DTOs;

namespace AccountingSystem.API.Services.Interfaces
{
    public interface IAuthService
    {
        Task<AuthResponseDTO> LoginAsync(LoginDTO loginDto);
        Task<AuthResponseDTO> CompleteMfaLoginAsync(LoginMfaDTO dto);
        Task<User> RegisterAsync(RegisterDTO registerDto);
        Task<AuthResponseDTO> RegisterCompanyAsync(CompanyRegisterDTO dto);
        Task<CurrentProfileDTO> GetCurrentProfileAsync(int userId);
        Task UpdateProfileAsync(int userId, UpdateProfileDTO dto);
        Task ChangePasswordAsync(int userId, ChangePasswordDTO dto);
        Task<MfaStatusDTO> GetMfaStatusAsync(int userId);
        Task<MfaSetupDTO> BeginAuthenticatorSetupAsync(int userId);
        Task<MfaSetupDTO> ResetAuthenticatorAsync(int userId, MfaReauthenticationDTO dto);
        Task<RecoveryCodesDTO> VerifyAuthenticatorSetupAsync(int userId, VerifyAuthenticatorSetupDTO dto);
        Task<RecoveryCodesDTO> RegenerateRecoveryCodesAsync(int userId, MfaReauthenticationDTO dto);
        Task DisableMfaAsync(int userId, MfaReauthenticationDTO dto);
        Task ConfirmEmailAsync(ConfirmEmailDTO dto);
        Task ResendConfirmationAsync(ResendConfirmationDTO dto);
        Task SendPasswordResetAsync(ForgotPasswordDTO dto);
        Task ResetPasswordAsync(ResetPasswordDTO dto);
    }
}
