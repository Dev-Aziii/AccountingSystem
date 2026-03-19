using AccountingSystem.API.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;

namespace AccountingSystem.API.Middleware
{
    public class JwtMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IConfiguration _configuration;

        public JwtMiddleware(RequestDelegate next, IConfiguration configuration)
        {
            _next = next;
            _configuration = configuration;
        }

        public async Task Invoke(HttpContext context)
        {
            var token = context.Request.Headers["Authorization"].FirstOrDefault()?.Split(' ').Last();
            if (!string.IsNullOrWhiteSpace(token))
            {
                AttachUserToContext(context, token);
            }

            await _next(context);
        }

        private void AttachUserToContext(HttpContext context, string token)
        {
            try
            {
                var tokenHandler = new JwtSecurityTokenHandler();
                var validationParameters = JwtSettingsHelper.CreateTokenValidationParameters(_configuration);

                tokenHandler.ValidateToken(token, validationParameters, out var validatedToken);

                if (validatedToken is not JwtSecurityToken jwtToken)
                {
                    return;
                }

                context.Items["User"] = jwtToken.Claims.FirstOrDefault(x => x.Type == "unique_name")?.Value;
                context.Items["Role"] = jwtToken.Claims.FirstOrDefault(x => x.Type == "role")?.Value;

                var userIdClaim = jwtToken.Claims.FirstOrDefault(x => x.Type == "UserId");
                if (userIdClaim != null)
                {
                    context.Items["UserId"] = userIdClaim.Value;
                }

                var companyIdClaim = jwtToken.Claims.FirstOrDefault(x => x.Type == "CompanyId");
                if (companyIdClaim != null)
                {
                    context.Items["CompanyId"] = companyIdClaim.Value;
                }
            }
            catch (SecurityTokenException)
            {
                // Invalid token; leave the request unauthenticated.
            }
            catch (ArgumentException)
            {
                // Malformed token; leave the request unauthenticated.
            }
        }
    }
}
