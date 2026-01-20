using AccountingSystem.Client.Auth;
using AccountingSystem.Client.Services.Interfaces;
using AccountingSystem.Shared.DTOs;
using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components.Authorization;
using System.Net.Http.Json;

namespace AccountingSystem.Client.Services
{
    public class ClientAuthService : IClientAuthService
    {
        private readonly HttpClient _httpClient;
        private readonly ILocalStorageService _localStorage;
        private readonly AuthenticationStateProvider _authStateProvider;

        public ClientAuthService(HttpClient httpClient, ILocalStorageService localStorage, AuthenticationStateProvider authStateProvider)
        {
            _httpClient = httpClient;
            _localStorage = localStorage;
            _authStateProvider = authStateProvider;
        }

        public async Task<AuthResponseDTO> Login(LoginDTO loginDto)
        {
            var response = await _httpClient.PostAsJsonAsync("api/auth/login", loginDto);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception(error);
            }

            var result = await response.Content.ReadFromJsonAsync<AuthResponseDTO>();

            // 1. Store Token
            await _localStorage.SetItemAsync("authToken", result.Token);

            // 2. Notify Blazor Auth State
            ((CustomAuthStateProvider)_authStateProvider).NotifyUserAuthentication(result.Token);

            return result;
        }

        public async Task Logout()
        {
            await _localStorage.RemoveItemAsync("authToken");
            ((CustomAuthStateProvider)_authStateProvider).NotifyUserLogout();
        }
    }
}