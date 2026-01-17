using AccountingSystem.Client.Auth;
using AccountingSystem.Shared.DTOs;
using Microsoft.AspNetCore.Components.Authorization;
using System.Net.Http.Json;

namespace AccountingSystem.Client.Services
{
    public class AuthService
    {
        private readonly ApiService _api;
        private readonly TokenStorageService _tokenService;
        private readonly AuthenticationStateProvider _authStateProvider;

        public AuthService(ApiService api, TokenStorageService tokenService, AuthenticationStateProvider authStateProvider)
        {
            _api = api;
            _tokenService = tokenService;
            _authStateProvider = authStateProvider;
        }

        public async Task<AuthResponseDTO> Login(LoginDTO loginDto)
        {
            // We use PostAsync directly here as we might not have a token yet
            var response = await _api.PostAsync("api/auth/login", loginDto);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception(error);
            }

            var result = await response.Content.ReadFromJsonAsync<AuthResponseDTO>();

            // 1. Store Token
            await _tokenService.SetTokenAsync(result.Token);

            // 2. Update Auth State
            ((CustomAuthStateProvider)_authStateProvider).NotifyUserAuthentication(result.Token);

            return result;
        }

        public async Task Logout()
        {
            await _tokenService.RemoveTokenAsync();
            ((CustomAuthStateProvider)_authStateProvider).NotifyUserLogout();
        }
    }
}