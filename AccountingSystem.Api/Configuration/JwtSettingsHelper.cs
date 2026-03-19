using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Text;

namespace AccountingSystem.API.Configuration
{
    internal static class JwtSettingsHelper
    {
        private const int DefaultExpiryMinutes = 60;
        private const int DefaultClockSkewSeconds = 60;

        internal static byte[] GetSigningKey(IConfiguration configuration)
        {
            var secret = configuration["JwtSettings:Secret"];
            if (StartupConfigurationValidator.IsMissingOrPlaceholder(secret))
            {
                throw new InvalidOperationException(
                    StartupConfigurationValidator.BuildMissingValueMessage("JwtSettings:Secret"));
            }

            return Encoding.ASCII.GetBytes(secret!.Trim());
        }

        internal static int GetExpiryMinutes(IConfiguration configuration)
        {
            var configuredValue = configuration.GetValue<int?>("JwtSettings:ExpiryMinutes");
            return configuredValue is > 0 ? configuredValue.Value : DefaultExpiryMinutes;
        }

        internal static TimeSpan GetClockSkew(IConfiguration configuration)
        {
            var configuredValue = configuration.GetValue<int?>("JwtSettings:ClockSkewSeconds");
            var seconds = configuredValue is >= 0 ? configuredValue.Value : DefaultClockSkewSeconds;
            return TimeSpan.FromSeconds(seconds);
        }

        internal static TokenValidationParameters CreateTokenValidationParameters(IConfiguration configuration)
        {
            return new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(GetSigningKey(configuration)),
                ValidateIssuer = true,
                ValidIssuer = configuration["JwtSettings:Issuer"],
                ValidateAudience = true,
                ValidAudience = configuration["JwtSettings:Audience"],
                ValidateLifetime = true,
                RequireExpirationTime = true,
                NameClaimType = ClaimTypes.Name,
                RoleClaimType = ClaimTypes.Role,
                ClockSkew = GetClockSkew(configuration)
            };
        }
    }
}
