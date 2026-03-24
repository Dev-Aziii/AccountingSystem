using System.ComponentModel.DataAnnotations;

namespace AccountingSystem.Shared.Validation
{
    public static class PasswordPolicy
    {
        public const int MinimumComplexPasswordLength = 12;
        public const int MinimumPassphraseLength = 16;
        public const int MinimumPassphraseWords = 3;
        public const int MaximumLength = 128;

        public const string Description =
            " ";

        public const string DefaultErrorMessage =
            "Password must be at least 12 characters and include at least 1 uppercase, 1 lowercase, 1 number, and 1 symbol.";

        public static bool TryValidate(string? password, out string errorMessage)
        {
            errorMessage = DefaultErrorMessage;

            if (string.IsNullOrWhiteSpace(password))
            {
                return false;
            }

            if (password.Length > MaximumLength)
            {
                errorMessage = $"Password must be {MaximumLength} characters or fewer.";
                return false;
            }

            if (IsComplexPassword(password) || IsPassphrase(password))
            {
                errorMessage = string.Empty;
                return true;
            }

            return false;
        }

        public static bool IsComplexPassword(string password)
        {
            if (password.Length < MinimumComplexPasswordLength || password.Length > MaximumLength)
            {
                return false;
            }

            var characterClassCount = 0;

            if (password.Any(char.IsUpper))
            {
                characterClassCount++;
            }

            if (password.Any(char.IsLower))
            {
                characterClassCount++;
            }

            if (password.Any(char.IsDigit))
            {
                characterClassCount++;
            }

            if (password.Any(character => !char.IsLetterOrDigit(character)))
            {
                characterClassCount++;
            }

            return characterClassCount >= 3;
        }

        public static bool IsPassphrase(string password)
        {
            if (password.Length < MinimumPassphraseLength || password.Length > MaximumLength)
            {
                return false;
            }

            var words = password.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            return words.Length >= MinimumPassphraseWords && words.All(word => word.Length >= 2);
        }
    }

    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter, AllowMultiple = false)]
    public sealed class StrongPasswordAttribute : ValidationAttribute
    {
        public StrongPasswordAttribute() : base(PasswordPolicy.DefaultErrorMessage)
        {
        }

        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            if (value is null)
            {
                return ValidationResult.Success;
            }

            if (value is not string password)
            {
                return new ValidationResult(PasswordPolicy.DefaultErrorMessage);
            }

            if (string.IsNullOrWhiteSpace(password))
            {
                return ValidationResult.Success;
            }

            return PasswordPolicy.TryValidate(password, out var errorMessage)
                ? ValidationResult.Success
                : new ValidationResult(errorMessage);
        }
    }
}
