using AccountingSystem.Client.Auth;
using AccountingSystem.Shared.DTOs;
using Microsoft.AspNetCore.Components.Authorization;
using System.Net.Http.Json;
using System.Text.Json;

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
            var response = await _api.PostAsync("api/auth/login", loginDto);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception(error);
            }

            var result = await response.Content.ReadFromJsonAsync<AuthResponseDTO>();

            if (result == null)
            {
                throw new Exception("Failed to deserialize authentication response");
            }

            // 1. Store Token
            await _tokenService.SetTokenAsync(result.Token);

            // 2. Update Auth State
            ((CustomAuthStateProvider)_authStateProvider).NotifyUserAuthentication(result.Token);

            return result;
        }

        public async Task<AuthResponseDTO> RegisterCompany(CompanyRegisterDTO registerDto)
        {
            var response = await _api.PostAsync("api/auth/register-company", registerDto);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception(ExtractApiErrorMessage(error, "Registration failed. Please try again."));
            }

            // Auto-login after registration
            var result = await response.Content.ReadFromJsonAsync<AuthResponseDTO>();

            if (result == null)
            {
                throw new Exception("Failed to deserialize registration response");
            }

            await _tokenService.SetTokenAsync(result.Token);
            ((CustomAuthStateProvider)_authStateProvider).NotifyUserAuthentication(result.Token);

            return result;
        }

        private static string ExtractApiErrorMessage(string rawContent, string fallbackMessage)
        {
            if (string.IsNullOrWhiteSpace(rawContent))
            {
                return fallbackMessage;
            }

            var trimmed = rawContent.Trim();

            try
            {
                using var document = JsonDocument.Parse(trimmed);
                var root = document.RootElement;

                if (root.ValueKind == JsonValueKind.Object)
                {
                    if (root.TryGetProperty("error", out var errorProperty) &&
                        errorProperty.ValueKind == JsonValueKind.String)
                    {
                        var errorMessage = errorProperty.GetString();
                        if (!string.IsNullOrWhiteSpace(errorMessage))
                        {
                            return errorMessage;
                        }
                    }

                    if (root.TryGetProperty("message", out var messageProperty) &&
                        messageProperty.ValueKind == JsonValueKind.String)
                    {
                        var message = messageProperty.GetString();
                        if (!string.IsNullOrWhiteSpace(message))
                        {
                            return message;
                        }
                    }
                }
                else if (root.ValueKind == JsonValueKind.String)
                {
                    var message = root.GetString();
                    if (!string.IsNullOrWhiteSpace(message))
                    {
                        return message;
                    }
                }
            }
            catch (JsonException)
            {
                // Return raw content when response is not JSON.
            }

            return trimmed;
        }

        public async Task Logout()
        {
            await _tokenService.RemoveTokenAsync();
            ((CustomAuthStateProvider)_authStateProvider).NotifyUserLogout();
        }

        //  Profile & Password Methods ---
        public async Task UpdateProfile(UpdateProfileDTO dto)
        {
            // ApiService handles the Bearer token attachment automatically
            var response = await _api.PutAsync("api/auth/profile", dto);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception(error);
            }
        }

        public async Task ChangePassword(ChangePasswordDTO dto)
        {
            var response = await _api.PutAsync("api/auth/password", dto);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception(error);
            }
        }
    }
}
