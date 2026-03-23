namespace AccountingSystem.API.Configuration
{
    internal static class StartupConfigurationValidator
    {
        internal const string PlaceholderValue = "__SET_VIA_USER_SECRETS_OR_ENV__";

        internal static void ValidateRequiredSettings(IConfiguration configuration, IWebHostEnvironment environment)
        {
            var missingKeys = new List<string>();
            var invalidKeys = new List<string>();
            var smtpKeys = new[]
            {
                "Smtp:Host",
                "Smtp:Port",
                "Smtp:Username",
                "Smtp:Password",
                "Smtp:FromAddress",
                "Smtp:FromName",
                "Smtp:EnableSsl"
            };
            var validateSmtpConfiguration = !environment.IsDevelopment() || HasAnyConfiguredValue(configuration, smtpKeys);

            CheckRequiredValue(configuration.GetConnectionString("DefaultConnection"), "ConnectionStrings:DefaultConnection", missingKeys);
            CheckRequiredValue(configuration["JwtSettings:Secret"], "JwtSettings:Secret", missingKeys);
            CheckRequiredValue(configuration["JwtSettings:Issuer"], "JwtSettings:Issuer", missingKeys);
            CheckRequiredValue(configuration["JwtSettings:Audience"], "JwtSettings:Audience", missingKeys);
            CheckRequiredValue(configuration["JwtSettings:ExpiryMinutes"], "JwtSettings:ExpiryMinutes", missingKeys);
            CheckRequiredValue(configuration["JwtSettings:ClockSkewSeconds"], "JwtSettings:ClockSkewSeconds", missingKeys);
            CheckRequiredValue(configuration["AuthSecurity:Lockout:MaxFailedAccessAttempts"], "AuthSecurity:Lockout:MaxFailedAccessAttempts", missingKeys);
            CheckRequiredValue(configuration["AuthSecurity:Lockout:LockoutMinutes"], "AuthSecurity:Lockout:LockoutMinutes", missingKeys);
            CheckRequiredValue(configuration["AuthSecurity:RateLimiting:Login:PermitLimit"], "AuthSecurity:RateLimiting:Login:PermitLimit", missingKeys);
            CheckRequiredValue(configuration["AuthSecurity:RateLimiting:Login:WindowSeconds"], "AuthSecurity:RateLimiting:Login:WindowSeconds", missingKeys);
            CheckRequiredValue(configuration["AuthSecurity:RateLimiting:RegisterCompany:PermitLimit"], "AuthSecurity:RateLimiting:RegisterCompany:PermitLimit", missingKeys);
            CheckRequiredValue(configuration["AuthSecurity:RateLimiting:RegisterCompany:WindowSeconds"], "AuthSecurity:RateLimiting:RegisterCompany:WindowSeconds", missingKeys);
            CheckRequiredValue(configuration["AuthSecurity:RateLimiting:ChangePassword:PermitLimit"], "AuthSecurity:RateLimiting:ChangePassword:PermitLimit", missingKeys);
            CheckRequiredValue(configuration["AuthSecurity:RateLimiting:ChangePassword:WindowSeconds"], "AuthSecurity:RateLimiting:ChangePassword:WindowSeconds", missingKeys);
            CheckRequiredValue(configuration["AuthSecurity:RateLimiting:ForgotPassword:PermitLimit"], "AuthSecurity:RateLimiting:ForgotPassword:PermitLimit", missingKeys);
            CheckRequiredValue(configuration["AuthSecurity:RateLimiting:ForgotPassword:WindowSeconds"], "AuthSecurity:RateLimiting:ForgotPassword:WindowSeconds", missingKeys);
            CheckRequiredValue(configuration["AuthSecurity:RateLimiting:ResetPassword:PermitLimit"], "AuthSecurity:RateLimiting:ResetPassword:PermitLimit", missingKeys);
            CheckRequiredValue(configuration["AuthSecurity:RateLimiting:ResetPassword:WindowSeconds"], "AuthSecurity:RateLimiting:ResetPassword:WindowSeconds", missingKeys);
            CheckRequiredValue(configuration["AuthSecurity:RateLimiting:ConfirmEmail:PermitLimit"], "AuthSecurity:RateLimiting:ConfirmEmail:PermitLimit", missingKeys);
            CheckRequiredValue(configuration["AuthSecurity:RateLimiting:ConfirmEmail:WindowSeconds"], "AuthSecurity:RateLimiting:ConfirmEmail:WindowSeconds", missingKeys);
            CheckRequiredValue(configuration["AuthSecurity:RateLimiting:ResendConfirmation:PermitLimit"], "AuthSecurity:RateLimiting:ResendConfirmation:PermitLimit", missingKeys);
            CheckRequiredValue(configuration["AuthSecurity:RateLimiting:ResendConfirmation:WindowSeconds"], "AuthSecurity:RateLimiting:ResendConfirmation:WindowSeconds", missingKeys);
            CheckRequiredValue(configuration["IdentityTokens:PasswordResetTokenLifespanMinutes"], "IdentityTokens:PasswordResetTokenLifespanMinutes", missingKeys);
            CheckRequiredValue(configuration["IdentityTokens:EmailConfirmationTokenLifespanMinutes"], "IdentityTokens:EmailConfirmationTokenLifespanMinutes", missingKeys);
            CheckRequiredValue(configuration["AppUrls:ClientBaseUrl"], "AppUrls:ClientBaseUrl", missingKeys);

            if (validateSmtpConfiguration)
            {
                CheckRequiredValue(configuration["Smtp:Host"], "Smtp:Host", missingKeys);
                CheckRequiredValue(configuration["Smtp:Port"], "Smtp:Port", missingKeys);
                CheckRequiredValue(configuration["Smtp:Username"], "Smtp:Username", missingKeys);
                CheckRequiredValue(configuration["Smtp:Password"], "Smtp:Password", missingKeys);
                CheckRequiredValue(configuration["Smtp:FromAddress"], "Smtp:FromAddress", missingKeys);
                CheckRequiredValue(configuration["Smtp:FromName"], "Smtp:FromName", missingKeys);
                CheckRequiredValue(configuration["Smtp:EnableSsl"], "Smtp:EnableSsl", missingKeys);
            }

            CheckPositiveInteger(configuration["JwtSettings:ExpiryMinutes"], "JwtSettings:ExpiryMinutes", invalidKeys);
            CheckNonNegativeInteger(configuration["JwtSettings:ClockSkewSeconds"], "JwtSettings:ClockSkewSeconds", invalidKeys);
            CheckPositiveInteger(configuration["AuthSecurity:Lockout:MaxFailedAccessAttempts"], "AuthSecurity:Lockout:MaxFailedAccessAttempts", invalidKeys);
            CheckPositiveInteger(configuration["AuthSecurity:Lockout:LockoutMinutes"], "AuthSecurity:Lockout:LockoutMinutes", invalidKeys);
            CheckPositiveInteger(configuration["AuthSecurity:RateLimiting:Login:PermitLimit"], "AuthSecurity:RateLimiting:Login:PermitLimit", invalidKeys);
            CheckPositiveInteger(configuration["AuthSecurity:RateLimiting:Login:WindowSeconds"], "AuthSecurity:RateLimiting:Login:WindowSeconds", invalidKeys);
            CheckPositiveInteger(configuration["AuthSecurity:RateLimiting:RegisterCompany:PermitLimit"], "AuthSecurity:RateLimiting:RegisterCompany:PermitLimit", invalidKeys);
            CheckPositiveInteger(configuration["AuthSecurity:RateLimiting:RegisterCompany:WindowSeconds"], "AuthSecurity:RateLimiting:RegisterCompany:WindowSeconds", invalidKeys);
            CheckPositiveInteger(configuration["AuthSecurity:RateLimiting:ChangePassword:PermitLimit"], "AuthSecurity:RateLimiting:ChangePassword:PermitLimit", invalidKeys);
            CheckPositiveInteger(configuration["AuthSecurity:RateLimiting:ChangePassword:WindowSeconds"], "AuthSecurity:RateLimiting:ChangePassword:WindowSeconds", invalidKeys);
            CheckPositiveInteger(configuration["AuthSecurity:RateLimiting:ForgotPassword:PermitLimit"], "AuthSecurity:RateLimiting:ForgotPassword:PermitLimit", invalidKeys);
            CheckPositiveInteger(configuration["AuthSecurity:RateLimiting:ForgotPassword:WindowSeconds"], "AuthSecurity:RateLimiting:ForgotPassword:WindowSeconds", invalidKeys);
            CheckPositiveInteger(configuration["AuthSecurity:RateLimiting:ResetPassword:PermitLimit"], "AuthSecurity:RateLimiting:ResetPassword:PermitLimit", invalidKeys);
            CheckPositiveInteger(configuration["AuthSecurity:RateLimiting:ResetPassword:WindowSeconds"], "AuthSecurity:RateLimiting:ResetPassword:WindowSeconds", invalidKeys);
            CheckPositiveInteger(configuration["AuthSecurity:RateLimiting:ConfirmEmail:PermitLimit"], "AuthSecurity:RateLimiting:ConfirmEmail:PermitLimit", invalidKeys);
            CheckPositiveInteger(configuration["AuthSecurity:RateLimiting:ConfirmEmail:WindowSeconds"], "AuthSecurity:RateLimiting:ConfirmEmail:WindowSeconds", invalidKeys);
            CheckPositiveInteger(configuration["AuthSecurity:RateLimiting:ResendConfirmation:PermitLimit"], "AuthSecurity:RateLimiting:ResendConfirmation:PermitLimit", invalidKeys);
            CheckPositiveInteger(configuration["AuthSecurity:RateLimiting:ResendConfirmation:WindowSeconds"], "AuthSecurity:RateLimiting:ResendConfirmation:WindowSeconds", invalidKeys);
            CheckPositiveInteger(configuration["IdentityTokens:PasswordResetTokenLifespanMinutes"], "IdentityTokens:PasswordResetTokenLifespanMinutes", invalidKeys);
            CheckPositiveInteger(configuration["IdentityTokens:EmailConfirmationTokenLifespanMinutes"], "IdentityTokens:EmailConfirmationTokenLifespanMinutes", invalidKeys);

            if (validateSmtpConfiguration)
            {
                CheckPositiveInteger(configuration["Smtp:Port"], "Smtp:Port", invalidKeys);
                CheckBoolean(configuration["Smtp:EnableSsl"], "Smtp:EnableSsl", invalidKeys);
            }

            ValidateSqlServerConnectionString(configuration.GetConnectionString("DefaultConnection"), invalidKeys);

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

        private static void CheckPositiveInteger(string? value, string configurationKey, ICollection<string> invalidKeys)
        {
            if (!IsMissingOrPlaceholder(value) &&
                (!int.TryParse(value, out var parsedValue) || parsedValue <= 0))
            {
                invalidKeys.Add($"{configurationKey} (must be a positive integer)");
            }
        }

        private static void CheckNonNegativeInteger(string? value, string configurationKey, ICollection<string> invalidKeys)
        {
            if (!IsMissingOrPlaceholder(value) &&
                (!int.TryParse(value, out var parsedValue) || parsedValue < 0))
            {
                invalidKeys.Add($"{configurationKey} (must be zero or a positive integer)");
            }
        }

        private static void CheckBoolean(string? value, string configurationKey, ICollection<string> invalidKeys)
        {
            if (!IsMissingOrPlaceholder(value) && !bool.TryParse(value, out _))
            {
                invalidKeys.Add($"{configurationKey} (must be 'true' or 'false')");
            }
        }

        private static bool HasAnyConfiguredValue(IConfiguration configuration, IEnumerable<string> configurationKeys)
        {
            return configurationKeys.Any(key => !IsMissingOrPlaceholder(configuration[key]));
        }

        private static void ValidateSqlServerConnectionString(string? connectionString, ICollection<string> invalidKeys)
        {
            if (IsMissingOrPlaceholder(connectionString))
            {
                return;
            }

            try
            {
                var builder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(connectionString);
                var dataSource = builder.DataSource?.Trim();

                if (string.IsNullOrWhiteSpace(dataSource))
                {
                    invalidKeys.Add("ConnectionStrings:DefaultConnection (missing Data Source / Server)");
                    return;
                }

                if (dataSource.Contains(@"\\", StringComparison.Ordinal) &&
                    !dataSource.StartsWith(@"\\", StringComparison.Ordinal) &&
                    !dataSource.StartsWith("np:", StringComparison.OrdinalIgnoreCase))
                {
                    invalidKeys.Add(
                        "ConnectionStrings:DefaultConnection (SQL Server instance name contains a doubled backslash at runtime; use 'Server=HOST\\INSTANCE' in user-secrets or environment variables)");
                }
            }
            catch (ArgumentException)
            {
                invalidKeys.Add("ConnectionStrings:DefaultConnection (invalid SQL Server connection string format)");
            }
        }
    }
}
