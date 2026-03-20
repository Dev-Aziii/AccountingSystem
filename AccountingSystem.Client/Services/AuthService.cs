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
            var response = await _api.PostAsync("api/auth/login", loginDto, requiresAuth: false);
            if (!response.IsSuccessStatusCode)
            {
                var rawContent = await response.Content.ReadAsStringAsync();
                throw new Exception(ApiErrorParser.Extract(rawContent, "Unable to sign in right now. Please try again."));
            }

            var result = await response.Content.ReadFromJsonAsync<AuthResponseDTO>();
            if (result == null)
            {
                throw new Exception("Failed to deserialize authentication response");
            }

            await _tokenService.SetTokenAsync(result.Token);
            ((CustomAuthStateProvider)_authStateProvider).NotifyUserAuthentication(result.Token);

            return result;
        }

        public async Task<AuthResponseDTO> RegisterCompany(CompanyRegisterDTO registerDto)
        {
            var response = await _api.PostAsync("api/auth/register-company", registerDto, requiresAuth: false);
            if (!response.IsSuccessStatusCode)
            {
                var rawContent = await response.Content.ReadAsStringAsync();
                throw new Exception(ApiErrorParser.Extract(rawContent, "Registration failed. Please try again."));
            }

            var result = await response.Content.ReadFromJsonAsync<AuthResponseDTO>();
            if (result == null)
            {
                throw new Exception("Failed to deserialize registration response");
            }

            await _tokenService.SetTokenAsync(result.Token);
            ((CustomAuthStateProvider)_authStateProvider).NotifyUserAuthentication(result.Token);

            return result;
        }

        public async Task Logout()
        {
            await _tokenService.RemoveTokenAsync();
            _api.ClearAuthHeader();
            ((CustomAuthStateProvider)_authStateProvider).NotifyUserLogout();
        }

        public async Task UpdateProfile(UpdateProfileDTO dto)
        {
            var response = await _api.PutAsync("api/auth/profile", dto);
            if (!response.IsSuccessStatusCode)
            {
                var rawContent = await response.Content.ReadAsStringAsync();
                throw new Exception(ApiErrorParser.Extract(rawContent, "Unable to update profile. Please try again."));
            }
        }

        public async Task<CurrentProfileDTO> GetCurrentProfile()
        {
            try
            {
                var profile = await _api.GetAsync<CurrentProfileDTO>("api/auth/profile");
                return profile ?? throw new Exception("Unable to load account details.");
            }
            catch (Exception ex) when (ex is not UnauthorizedAccessException)
            {
                throw new Exception("Unable to load account details.", ex);
            }
        }

        public async Task ChangePassword(ChangePasswordDTO dto)
        {
            var response = await _api.PutAsync("api/auth/password", dto);
            if (!response.IsSuccessStatusCode)
            {
                var rawContent = await response.Content.ReadAsStringAsync();
                throw new Exception(ApiErrorParser.Extract(rawContent, "Unable to change password. Please try again."));
            }
        }

        public async Task RequestPasswordReset(ForgotPasswordDTO dto)
        {
            var response = await _api.PostAsync("api/auth/forgot-password", dto, requiresAuth: false);
            if (!response.IsSuccessStatusCode)
            {
                var rawContent = await response.Content.ReadAsStringAsync();
                throw new Exception(ApiErrorParser.Extract(rawContent, "Unable to send password reset email. Please try again."));
            }
        }

        public async Task ResetPassword(ResetPasswordDTO dto)
        {
            var response = await _api.PostAsync("api/auth/reset-password", dto, requiresAuth: false);
            if (!response.IsSuccessStatusCode)
            {
                var rawContent = await response.Content.ReadAsStringAsync();
                throw new Exception(ApiErrorParser.Extract(rawContent, "Unable to reset password. Please try again."));
            }
        }
    }
}
