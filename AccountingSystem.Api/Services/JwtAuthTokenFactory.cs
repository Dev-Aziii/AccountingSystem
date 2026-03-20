using AccountingSystem.API.Configuration;
using AccountingSystem.API.Security;
using AccountingSystem.API.Services.Interfaces;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace AccountingSystem.API.Services
{
    public class JwtAuthTokenFactory : IAuthTokenFactory
    {
        private readonly IConfiguration _configuration;

        public JwtAuthTokenFactory(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public AuthTokenResult Create(AuthTokenContext context)
        {
            var key = JwtSettingsHelper.GetSigningKey(_configuration);
            var expiresAt = DateTime.UtcNow.AddMinutes(JwtSettingsHelper.GetExpiryMinutes(_configuration));
            var tokenHandler = new JwtSecurityTokenHandler();
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.Name, context.Email),
                    new Claim(ClaimTypes.Role, context.Role),
                    new Claim("UserId", context.UserId.ToString()),
                    new Claim("role", context.Role),
                    new Claim("FullName", string.IsNullOrWhiteSpace(context.FullName) ? context.Email : context.FullName),
                    new Claim("CompanyId", context.CompanyId.ToString()),
                    new Claim("CompanyName", context.CompanyName)
                }),
                Expires = expiresAt,
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature),
                Issuer = _configuration["JwtSettings:Issuer"],
                Audience = _configuration["JwtSettings:Audience"]
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return new AuthTokenResult(tokenHandler.WriteToken(token), expiresAt);
        }
    }
}
