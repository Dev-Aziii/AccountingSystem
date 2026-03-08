using AccountingSystem.API.Models;
using AccountingSystem.Shared.DTOs;

namespace AccountingSystem.API.Services.Interfaces
{
    public interface IAuthService
    {
        Task<AuthResponseDTO> LoginAsync(LoginDTO loginDto);
        Task<User> RegisterAsync(RegisterDTO registerDto);
        Task<AuthResponseDTO> RegisterCompanyAsync(CompanyRegisterDTO dto);
        Task UpdateProfileAsync(int userId, UpdateProfileDTO dto);
        Task ChangePasswordAsync(int userId, ChangePasswordDTO dto);
    }
}