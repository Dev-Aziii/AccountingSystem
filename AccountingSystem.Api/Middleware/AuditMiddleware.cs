using AccountingSystem.API.Data;
using AccountingSystem.API.Models;
using System.Text;

namespace AccountingSystem.API.Middleware
{
    public class AuditMiddleware
    {
        private readonly RequestDelegate _next;

        public AuditMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task Invoke(HttpContext context, AccountingDbContext dbContext)
        {
            // Only log state-changing methods
            var method = context.Request.Method;
            if (method == "POST" || method == "PUT" || method == "DELETE")
            {
                await LogAuditAsync(context, dbContext);
            }

            await _next(context);
        }

        private async Task LogAuditAsync(HttpContext context, AccountingDbContext dbContext)
        {
            try
            {
                context.Request.EnableBuffering();

                string bodyContent = "";
                using (var reader = new StreamReader(context.Request.Body, Encoding.UTF8, true, 1024, true))
                {
                    bodyContent = await reader.ReadToEndAsync();
                }

                context.Request.Body.Position = 0;

                // FIX: Retrieve the numeric UserId stored by JwtMiddleware
                int? userId = null;
                if (context.Items["UserId"] is string userIdStr && int.TryParse(userIdStr, out int parsedId))
                {
                    userId = parsedId;
                }

                // Identify User (Email) for fallback or debugging if needed, but we use ID for FK
                // var userEmail = context.Items["User"]?.ToString() ?? "Anonymous";

                var auditLog = new AuditLog
                {
                    UserId = userId, // Now assigning int? (Correct type)
                    Action = context.Request.Method,
                    EntityName = context.Request.Path,
                    EntityId = "N/A",
                    Timestamp = DateTime.UtcNow,
                    Changes = bodyContent.Length > 2000 ? bodyContent.Substring(0, 2000) : bodyContent
                };

                dbContext.AuditLogs.Add(auditLog);
                await dbContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Audit Logging Failed: {ex.Message}");
            }
        }
    }
}