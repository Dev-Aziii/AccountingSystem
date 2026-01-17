using AccountingSystem.Shared.DTOs;

namespace AccountingSystem.Client.Services
{
    public interface IClientAuthService
    {
        Task<AuthResponseDTO> Login(LoginDTO loginDto);
        Task Logout();
    }
}