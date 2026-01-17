using AccountingSystem.Shared.DTOs;
using AccountingSystem.API.Models;

namespace AccountingSystem.API.Services.Interfaces
{
    public interface IAuthService
    {
        Task<AuthResponseDTO> LoginAsync(LoginDTO loginDto);
        Task<User> RegisterAsync(RegisterDTO registerDto);
    }
}