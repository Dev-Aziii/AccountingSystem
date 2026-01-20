using AccountingSystem.Shared.DTOs;

namespace AccountingSystem.Client.Services.Interfaces
{
    public interface IClientAuthService
    {
        Task<AuthResponseDTO> Login(LoginDTO loginDto);
        Task Logout();
    }
}