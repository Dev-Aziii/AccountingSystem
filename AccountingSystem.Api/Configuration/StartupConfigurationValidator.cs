namespace AccountingSystem.API.Configuration
{
    internal static class StartupConfigurationValidator
    {
        internal const string PlaceholderValue = "__SET_VIA_USER_SECRETS_OR_ENV__";

        internal static void ValidateRequiredSettings(IConfiguration configuration, IWebHostEnvironment environment)
        {
            var missingKeys = new List<string>();
            var invalidKeys = new List<string>();

            CheckRequiredValue(configuration.GetConnectionString("DefaultConnection"), "ConnectionStrings:DefaultConnection", missingKeys);
            CheckRequiredValue(configuration["JwtSettings:Secret"], "JwtSettings:Secret", missingKeys);
            CheckRequiredValue(configuration["JwtSettings:Issuer"], "JwtSettings:Issuer", missingKeys);
            CheckRequiredValue(configuration["JwtSettings:Audience"], "JwtSettings:Audience", missingKeys);
            CheckRequiredValue(configuration["JwtSettings:ExpiryMinutes"], "JwtSettings:ExpiryMinutes", missingKeys);

            var expiryMinutes = configuration["JwtSettings:ExpiryMinutes"];
            if (!IsMissingOrPlaceholder(expiryMinutes) &&
                (!int.TryParse(expiryMinutes, out var parsedMinutes) || parsedMinutes <= 0))
            {
                invalidKeys.Add("JwtSettings:ExpiryMinutes (must be a positive integer)");
            }

            if (!environment.IsDevelopment())
            {
                CheckRequiredValue(configuration["PayMongo:SecretKey"], "PayMongo:SecretKey", missingKeys);
                CheckRequiredValue(configuration["Recaptcha:SecretKey"], "Recaptcha:SecretKey", missingKeys);
            }

            if (missingKeys.Count == 0 && invalidKeys.Count == 0)
            {
                return;
            }

            var environmentDescription = environment.IsDevelopment()
                ? "Development"
                : $"non-development ('{environment.EnvironmentName}')";

            var details = new List<string>();
            if (missingKeys.Count > 0)
            {
                details.Add($"Missing: {string.Join(", ", missingKeys)}.");
            }

            if (invalidKeys.Count > 0)
            {
                details.Add($"Invalid: {string.Join(", ", invalidKeys)}.");
            }

            throw new InvalidOperationException(
                $"Required configuration is missing or invalid while starting the API in {environmentDescription}. " +
                $"{string.Join(" ", details)} " +
                "Use 'dotnet user-secrets' while developing or set environment variables locally. " +
                "In deployed environments, inject values via environment variables or a secret store.");
        }

        internal static bool IsMissingOrPlaceholder(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ||
                   string.Equals(value.Trim(), PlaceholderValue, StringComparison.Ordinal);
        }

        internal static string BuildMissingValueMessage(string configurationKey)
        {
            return $"{configurationKey} is not configured. Configure it via 'dotnet user-secrets' in Development or via environment variables / secret store in deployed environments.";
        }

        private static void CheckRequiredValue(string? value, string configurationKey, ICollection<string> missingKeys)
        {
            if (IsMissingOrPlaceholder(value))
            {
                missingKeys.Add(configurationKey);
            }
        }
    }
}
