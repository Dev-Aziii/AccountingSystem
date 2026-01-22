using AccountingSystem.API.Models;
using AccountingSystem.Shared.DTOs;

namespace AccountingSystem.API.Services.Interfaces
{
    public interface IAuthService
    {
        Task<AuthResponseDTO> LoginAsync(LoginDTO loginDto);
        Task<User> RegisterAsync(RegisterDTO registerDto);

        // NEW: Register a full company tenant
        Task<AuthResponseDTO> RegisterCompanyAsync(CompanyRegisterDTO dto);
    }
}