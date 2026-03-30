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

            if (!result.RequiresTwoFactor && !string.IsNullOrWhiteSpace(result.Token))
            {
                await _tokenService.SetTokenAsync(result.Token);
                ((CustomAuthStateProvider)_authStateProvider).NotifyUserAuthentication(result.Token);
            }

            return result;
        }

        public async Task<AuthResponseDTO> CompleteMfaLogin(LoginMfaDTO dto)
        {
            var response = await _api.PostAsync("api/auth/login/mfa", dto, requiresAuth: false);
            if (!response.IsSuccessStatusCode)
            {
                var rawContent = await response.Content.ReadAsStringAsync();
                throw new Exception(ApiErrorParser.Extract(rawContent, "Unable to verify the authenticator code. Please try again."));
            }

            var result = await response.Content.ReadFromJsonAsync<AuthResponseDTO>();
            if (result == null)
            {
                throw new Exception("Failed to deserialize MFA authentication response");
            }

            if (!string.IsNullOrWhiteSpace(result.Token))
            {
                await _tokenService.SetTokenAsync(result.Token);
                ((CustomAuthStateProvider)_authStateProvider).NotifyUserAuthentication(result.Token);
            }

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

            if (!string.IsNullOrWhiteSpace(result.Token))
            {
                await _tokenService.SetTokenAsync(result.Token);
                ((CustomAuthStateProvider)_authStateProvider).NotifyUserAuthentication(result.Token);
            }

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

        public async Task<ConfirmEmailResultDTO> ConfirmEmail(ConfirmEmailDTO dto)
        {
            var response = await _api.PostAsync("api/auth/confirm-email", dto, requiresAuth: false);
            if (!response.IsSuccessStatusCode)
            {
                var rawContent = await response.Content.ReadAsStringAsync();
                throw new Exception(ApiErrorParser.Extract(rawContent, "Unable to confirm email. Please try again."));
            }

            var result = await response.Content.ReadFromJsonAsync<ConfirmEmailResultDTO>();
            return result ?? throw new Exception("Failed to deserialize email confirmation response.");
        }

        public async Task ResendConfirmation(ResendConfirmationDTO dto)
        {
            var response = await _api.PostAsync("api/auth/resend-confirmation", dto, requiresAuth: false);
            if (!response.IsSuccessStatusCode)
            {
                var rawContent = await response.Content.ReadAsStringAsync();
                throw new Exception(ApiErrorParser.Extract(rawContent, "Unable to resend confirmation email. Please try again."));
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

        public async Task<MfaStatusDTO> GetMfaStatus()
        {
            try
            {
                var status = await _api.GetAsync<MfaStatusDTO>("api/auth/mfa");
                return status ?? throw new Exception("Unable to load MFA status.");
            }
            catch (Exception ex) when (ex is not UnauthorizedAccessException)
            {
                throw new Exception("Unable to load MFA status.", ex);
            }
        }

        public async Task<MfaSetupDTO> BeginAuthenticatorSetup()
        {
            var response = await _api.PostAsync("api/auth/mfa/authenticator/setup", new { });
            if (!response.IsSuccessStatusCode)
            {
                var rawContent = await response.Content.ReadAsStringAsync();
                throw new Exception(ApiErrorParser.Extract(rawContent, "Unable to start MFA setup. Please try again."));
            }

            var result = await response.Content.ReadFromJsonAsync<MfaSetupDTO>();
            return result ?? throw new Exception("Failed to deserialize MFA setup response.");
        }

        public async Task<MfaSetupDTO> ResetAuthenticator(MfaReauthenticationDTO dto)
        {
            var response = await _api.PostAsync("api/auth/mfa/authenticator/reset", dto);
            if (!response.IsSuccessStatusCode)
            {
                var rawContent = await response.Content.ReadAsStringAsync();
                throw new Exception(ApiErrorParser.Extract(rawContent, "Unable to reset the authenticator app. Please try again."));
            }

            var result = await response.Content.ReadFromJsonAsync<MfaSetupDTO>();
            return result ?? throw new Exception("Failed to deserialize authenticator reset response.");
        }

        public async Task<RecoveryCodesDTO> VerifyAuthenticatorSetup(VerifyAuthenticatorSetupDTO dto)
        {
            var response = await _api.PostAsync("api/auth/mfa/authenticator/verify", dto);
            if (!response.IsSuccessStatusCode)
            {
                var rawContent = await response.Content.ReadAsStringAsync();
                throw new Exception(ApiErrorParser.Extract(rawContent, "Unable to enable MFA. Please try again."));
            }

            var result = await response.Content.ReadFromJsonAsync<RecoveryCodesDTO>();
            return result ?? throw new Exception("Failed to deserialize recovery code response.");
        }

        public async Task<RecoveryCodesDTO> RegenerateRecoveryCodes(MfaReauthenticationDTO dto)
        {
            var response = await _api.PostAsync("api/auth/mfa/recovery-codes/regenerate", dto);
            if (!response.IsSuccessStatusCode)
            {
                var rawContent = await response.Content.ReadAsStringAsync();
                throw new Exception(ApiErrorParser.Extract(rawContent, "Unable to regenerate recovery codes. Please try again."));
            }

            var result = await response.Content.ReadFromJsonAsync<RecoveryCodesDTO>();
            return result ?? throw new Exception("Failed to deserialize recovery code response.");
        }

        public async Task DisableMfa(MfaReauthenticationDTO dto)
        {
            var response = await _api.PostAsync("api/auth/mfa/disable", dto);
            if (!response.IsSuccessStatusCode)
            {
                var rawContent = await response.Content.ReadAsStringAsync();
                throw new Exception(ApiErrorParser.Extract(rawContent, "Unable to disable MFA. Please try again."));
            }
        }
    }
}
