using AccountingSystem.API.Configuration;
using AccountingSystem.API.Security;
using AccountingSystem.API.Services.Interfaces;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace AccountingSystem.API.Services
{
    public class LoginChallengeTokenService : ILoginChallengeTokenService
    {
        private const string PurposeClaimType = "purpose";
        private const string PurposeValue = "mfa-login-challenge";
        private const string IdentityUserIdClaimType = "IdentityUserId";
        private const string LegacyUserIdClaimType = "LegacyUserId";

        private readonly IConfiguration _configuration;
        private readonly TimeSpan _lifetime;

        public LoginChallengeTokenService(
            IConfiguration configuration,
            IOptions<MfaSettings> settings)
        {
            _configuration = configuration;
            var lifetimeMinutes = settings.Value.LoginChallengeLifespanMinutes > 0
                ? settings.Value.LoginChallengeLifespanMinutes
                : 5;
            _lifetime = TimeSpan.FromMinutes(lifetimeMinutes);
        }

        public string Create(LoginChallengeTokenContext context)
        {
            var key = JwtSettingsHelper.GetSigningKey(_configuration);
            var tokenHandler = new JwtSecurityTokenHandler();
            var descriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[]
                {
                    new Claim(PurposeClaimType, PurposeValue),
                    new Claim(IdentityUserIdClaimType, context.IdentityUserId.ToString()),
                    new Claim(LegacyUserIdClaimType, context.LegacyUserId.ToString())
                }),
                NotBefore = DateTime.UtcNow,
                Expires = DateTime.UtcNow.Add(_lifetime),
                Issuer = _configuration["JwtSettings:Issuer"],
                Audience = BuildAudience(),
                SigningCredentials = new SigningCredentials(
                    new SymmetricSecurityKey(key),
                    SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(descriptor);
            return tokenHandler.WriteToken(token);
        }

        public LoginChallengeTokenPayload Validate(string token)
        {
            try
            {
                var tokenHandler = new JwtSecurityTokenHandler();
                var parameters = JwtSettingsHelper.CreateTokenValidationParameters(_configuration);
                parameters.ValidAudience = BuildAudience();

                var principal = tokenHandler.ValidateToken(token, parameters, out _);
                var purpose = principal.FindFirst(PurposeClaimType)?.Value;
                if (!string.Equals(purpose, PurposeValue, StringComparison.Ordinal))
                {
                    throw new SecurityTokenException("Invalid challenge token purpose.");
                }

                var identityUserId = ParseRequiredIntClaim(principal, IdentityUserIdClaimType);
                var legacyUserId = ParseRequiredIntClaim(principal, LegacyUserIdClaimType);

                return new LoginChallengeTokenPayload(identityUserId, legacyUserId, purpose!);
            }
            catch (Exception ex) when (ex is SecurityTokenException || ex is ArgumentException)
            {
                throw new AuthFailureException(
                    "MfaChallengeInvalid",
                    "The sign-in verification session is invalid or expired. Please sign in again.");
            }
        }

        private string BuildAudience()
        {
            var audience = _configuration["JwtSettings:Audience"]?.Trim();
            return string.IsNullOrWhiteSpace(audience)
                ? "mfa-login-challenge"
                : $"{audience}:mfa-challenge";
        }

        private static int ParseRequiredIntClaim(ClaimsPrincipal principal, string claimType)
        {
            var claimValue = principal.FindFirst(claimType)?.Value;
            if (!int.TryParse(claimValue, out var parsedValue))
            {
                throw new SecurityTokenException($"Claim '{claimType}' is missing or invalid.");
            }

            return parsedValue;
        }
    }
}
