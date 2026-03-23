using AccountingSystem.API.Models;
using AccountingSystem.Shared.DTOs;

namespace AccountingSystem.API.Services.Interfaces
{
    public interface IAuthService
    {
        Task<AuthResponseDTO> LoginAsync(LoginDTO loginDto);
        Task<User> RegisterAsync(RegisterDTO registerDto);
        Task<AuthResponseDTO> RegisterCompanyAsync(CompanyRegisterDTO dto);
        Task<CurrentProfileDTO> GetCurrentProfileAsync(int userId);
        Task UpdateProfileAsync(int userId, UpdateProfileDTO dto);
        Task ChangePasswordAsync(int userId, ChangePasswordDTO dto);
        Task ConfirmEmailAsync(ConfirmEmailDTO dto);
        Task ResendConfirmationAsync(ResendConfirmationDTO dto);
        Task SendPasswordResetAsync(ForgotPasswordDTO dto);
        Task ResetPasswordAsync(ResetPasswordDTO dto);
    }
}
