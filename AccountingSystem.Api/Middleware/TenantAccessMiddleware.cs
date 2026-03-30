using AccountingSystem.API.Data;
using AccountingSystem.Shared.Security;
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
            if (!ApplicationAuthorizationScopeEvaluator.IsAuthenticated(context.User))
            {
                await _next(context);
                return;
            }

            if (ApplicationAuthorizationScopeEvaluator.IsSuperAdmin(context.User))
            {
                await _next(context);
                return;
            }

            if (!ApplicationAuthorizationScopeEvaluator.IsTenantScopedPrincipal(context.User))
            {
                await _next(context);
                return;
            }

            if (!ApplicationAuthorizationScopeEvaluator.TryGetCompanyId(context.User, out var companyId))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new { message = "Tenant access requires a valid company context." });
                return;
            }

            if (context.User.FindFirst("UserId") is { Value: var userIdValue } && int.TryParse(userIdValue, out var userId))
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

            var company = await dbContext.Companies
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(c => c.Id == companyId);

            if (company == null)
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new { message = "Tenant access requires a valid company context." });
                return;
            }

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

            await _next(context);
        }
    }
}
