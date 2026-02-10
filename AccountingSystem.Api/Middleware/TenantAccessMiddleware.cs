using AccountingSystem.API.Data;
using Microsoft.EntityFrameworkCore;

namespace AccountingSystem.API.Middleware
{
    /// <summary>
    /// Server-side enforcement: Blocks all API requests from users whose company 
    /// or personal account is suspended/blocked. SuperAdmin is always exempt.
    /// </summary>
    public class TenantAccessMiddleware
    {
        private readonly RequestDelegate _next;

        public TenantAccessMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task Invoke(HttpContext context, AccountingDbContext dbContext)
        {
            // Skip for unauthenticated requests (login, register, etc.)
            var role = context.Items["Role"] as string;
            if (string.IsNullOrEmpty(role))
            {
                await _next(context);
                return;
            }

            // SuperAdmin is always allowed through
            if (role == "SuperAdmin")
            {
                await _next(context);
                return;
            }

            // Check user status
            if (context.Items["UserId"] is string userIdStr && int.TryParse(userIdStr, out int userId))
            {
                var user = await dbContext.Users
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(u => u.Id == userId);

                if (user != null && user.Status == "Blocked")
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    await context.Response.WriteAsJsonAsync(new { message = "Your account has been blocked. Please contact the System Administrator." });
                    return;
                }
            }

            // Check company status
            if (context.Items["CompanyId"] is string companyIdStr && int.TryParse(companyIdStr, out int companyId))
            {
                var company = await dbContext.Companies
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(c => c.Id == companyId);

                if (company != null)
                {
                    if (company.Status == "Blocked")
                    {
                        context.Response.StatusCode = StatusCodes.Status403Forbidden;
                        await context.Response.WriteAsJsonAsync(new { message = "This organization has been permanently blocked." });
                        return;
                    }

                    if (company.Status == "Suspended" || !company.IsActive)
                    {
                        context.Response.StatusCode = StatusCodes.Status403Forbidden;
                        await context.Response.WriteAsJsonAsync(new { message = "This organization's access has been suspended." });
                        return;
                    }
                }
            }

            await _next(context);
        }
    }
}
