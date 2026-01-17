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
                // 1. Enable buffering so we can read the body and reset the stream for the Controller
                context.Request.EnableBuffering();

                // 2. Read Request Body
                string bodyContent = "";
                using (var reader = new StreamReader(context.Request.Body, Encoding.UTF8, true, 1024, true))
                {
                    bodyContent = await reader.ReadToEndAsync();
                }

                // 3. Reset Stream Position
                context.Request.Body.Position = 0;

                // 4. Identify User (Attached by JwtMiddleware)
                var user = context.Items["User"]?.ToString() ?? "Anonymous";

                // 5. Create Log Entry
                var auditLog = new AuditLog
                {
                    UserId = user,
                    Action = context.Request.Method,
                    EntityName = context.Request.Path, // e.g., /api/ledger/journal
                    EntityId = "N/A", // Can be refined to parse ID from route
                    Timestamp = DateTime.UtcNow,
                    Changes = bodyContent.Length > 2000 ? bodyContent.Substring(0, 2000) : bodyContent // Truncate if too long
                };

                // 6. Save to DB
                dbContext.AuditLogs.Add(auditLog);
                await dbContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                // Fail silently: Logging should not break the business flow
                Console.WriteLine($"Audit Logging Failed: {ex.Message}");
            }
        }
    }
}