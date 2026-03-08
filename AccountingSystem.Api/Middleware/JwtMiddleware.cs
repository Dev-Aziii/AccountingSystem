using AccountingSystem.API.Services.Interfaces;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Text;

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
            var token = context.Request.Headers["Authorization"].FirstOrDefault()?.Split(" ").Last();
            if (token != null)
                AttachUserToContext(context, token);

            await _next(context);
        }

        private void AttachUserToContext(HttpContext context, string token)
        {
            try
            {
                var tokenHandler = new JwtSecurityTokenHandler();

                var secret = _configuration["JwtSettings:Secret"];
                if (string.IsNullOrEmpty(secret))
                {
                    return; // Exit if secret is not configured
                }

                var key = Encoding.ASCII.GetBytes(secret);

                tokenHandler.ValidateToken(token, new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = true,
                    ValidIssuer = _configuration["JwtSettings:Issuer"],
                    ValidateAudience = true,
                    ValidAudience = _configuration["JwtSettings:Audience"],
                    ClockSkew = TimeSpan.Zero
                }, out SecurityToken validatedToken);

                var jwtToken = (JwtSecurityToken)validatedToken;

                // Claims
                context.Items["User"] = jwtToken.Claims.First(x => x.Type == "unique_name").Value;
                context.Items["Role"] = jwtToken.Claims.First(x => x.Type == "role").Value;

                var userIdClaim = jwtToken.Claims.FirstOrDefault(x => x.Type == "UserId");
                if (userIdClaim != null)
                {
                    context.Items["UserId"] = userIdClaim.Value;
                }

                // Extract CompanyId
                var companyIdClaim = jwtToken.Claims.FirstOrDefault(x => x.Type == "CompanyId");
                if (companyIdClaim != null)
                {
                    context.Items["CompanyId"] = companyIdClaim.Value;
                }
            }
            catch
            {
                // Do nothing if JWT validation fails, user is not attached to context
            }
        }
    }
}