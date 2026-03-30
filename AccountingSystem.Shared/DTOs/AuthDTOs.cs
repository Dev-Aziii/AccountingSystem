using AccountingSystem.Shared.Validation;
using System.ComponentModel.DataAnnotations;

namespace AccountingSystem.Shared.DTOs
{
    //  Profile & Password Management ---
    public class UpdateProfileDTO
    {
        [Required]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;
    }

    public class CurrentProfileDTO
    {
        public string FullName { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public string Role { get; set; } = string.Empty;

        public int CompanyId { get; set; }

        public string CompanyName { get; set; } = string.Empty;
    }

    public class ChangePasswordDTO
    {
        [Required]
        public string CurrentPassword { get; set; } = string.Empty;

        [Required]
        [StrongPassword]
        public string NewPassword { get; set; } = string.Empty;

        [Required]
        [Compare(nameof(NewPassword), ErrorMessage = "Passwords do not match.")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    public class ForgotPasswordDTO
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;
    }

    public class ResetPasswordDTO
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string Token { get; set; } = string.Empty;

        [Required]
        [StrongPassword]
        public string NewPassword { get; set; } = string.Empty;

        [Required]
        [Compare(nameof(NewPassword), ErrorMessage = "Passwords do not match.")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    public class ConfirmEmailDTO
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string Token { get; set; } = string.Empty;
    }

    public class ResendConfirmationDTO
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;
    }

    // --- Existing DTOs ---
    public class CompanyRegisterDTO
    {
        [Required]
        public string CompanyName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        public string AdminEmail { get; set; } = string.Empty;

        [Required]
        public string AdminFullName { get; set; } = string.Empty;

        [Required]
        [StrongPassword]
        public string Password { get; set; } = string.Empty;

        public string RecaptchaToken { get; set; } = string.Empty;
    }

    public class RegisterDTO
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        [StrongPassword]
        public string Password { get; set; } = string.Empty;

        [Required]
        public string FullName { get; set; } = string.Empty;

        [Required]
        public string RoleName { get; set; } = string.Empty;
    }

    public class LoginDTO
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string Password { get; set; } = string.Empty;
    }

    public class LoginMfaDTO
    {
        [Required]
        public string ChallengeToken { get; set; } = string.Empty;

        public string TwoFactorCode { get; set; } = string.Empty;

        public string RecoveryCode { get; set; } = string.Empty;
    }

    public class AuthResponseDTO
    {
        public string Token { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public int CompanyId { get; set; }
        public string CompanyName { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
        public bool RequiresEmailConfirmation { get; set; }
        public bool RequiresTwoFactor { get; set; }
        public string TwoFactorChallengeToken { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }

    public class MfaStatusDTO
    {
        public bool IsTwoFactorEnabled { get; set; }

        public bool HasAuthenticatorKey { get; set; }

        public int RecoveryCodesLeft { get; set; }
    }

    public class MfaSetupDTO
    {
        public bool IsTwoFactorEnabled { get; set; }

        public string SharedKey { get; set; } = string.Empty;

        public string AuthenticatorUri { get; set; } = string.Empty;
    }

    public class VerifyAuthenticatorSetupDTO
    {
        [Required]
        public string Code { get; set; } = string.Empty;
    }

    public class MfaReauthenticationDTO
    {
        public string CurrentPassword { get; set; } = string.Empty;

        public string TwoFactorCode { get; set; } = string.Empty;

        public string RecoveryCode { get; set; } = string.Empty;
    }

    public class RecoveryCodesDTO
    {
        public List<string> RecoveryCodes { get; set; } = new();
    }
}
