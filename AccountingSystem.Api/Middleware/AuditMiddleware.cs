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

                int? userId = null;
                if (context.Items["UserId"] is string userIdStr && int.TryParse(userIdStr, out int parsedId))
                {
                    userId = parsedId;
                }

                int companyId = 0;
                if (context.Items["CompanyId"] is string companyIdStr && int.TryParse(companyIdStr, out int parsedCId))
                {
                    companyId = parsedCId;
                }

                // Determine Action Name based on Method and Path
                string action = context.Request.Method;
                string path = context.Request.Path.Value?.ToLower() ?? "";

                if (path.Contains("/auth/login") && action == "POST")
                {
                    action = "LOGIN";
                    bodyContent = "[Credentials Hidden]";
                }
                // User Management
                else if (path.Contains("/api/users"))
                {
                    if (path.EndsWith("/restore")) action = "USER-RESTORE";
                    else if (action == "POST") action = "USER-CREATE";
                    else if (action == "DELETE") action = "USER-ARCHIVE";
                }
                // Customer Management
                else if (path.Contains("/receivables/customers"))
                {
                    if (path.EndsWith("/restore")) action = "CUSTOMER-RESTORE";
                    else if (action == "POST") action = "CUSTOMER-CREATE";
                    else if (action == "PUT") action = "CUSTOMER-UPDATE";
                    else if (action == "DELETE") action = "CUSTOMER-ARCHIVE";
                }
                // Vendor Management
                else if (path.Contains("/payables/vendors"))
                {
                    if (path.EndsWith("/restore")) action = "VENDOR-RESTORE";
                    else if (action == "POST") action = "VENDOR-CREATE";
                    else if (action == "PUT") action = "VENDOR-UPDATE";
                    else if (action == "DELETE") action = "VENDOR-ARCHIVE";
                }
                // Chart of Accounts
                else if (path.Contains("/ledger/accounts"))
                {
                    if (path.EndsWith("/restore")) action = "ACCOUNT-RESTORE";
                    else if (action == "POST") action = "ACCOUNT-CREATE";
                    else if (action == "PUT") action = "ACCOUNT-UPDATE";
                    else if (action == "DELETE") action = "ACCOUNT-ARCHIVE";
                }
                // Transactions
                else if (path.Contains("/bill") && action == "POST")
                {
                    if (path.Contains("/pay")) action = "BILL-PAY";
                    else action = "BILL-CREATE";
                }
                else if (path.Contains("/invoice") && action == "POST")
                {
                    if (path.Contains("/receive")) action = "INVOICE-PAYMENT";
                    else action = "INVOICE-CREATE";
                }
                else if (path.Contains("/journal") && action == "POST")
                {
                    action = "JOURNAL-ENTRY";
                }

                var auditLog = new AuditLog
                {
                    UserId = userId,
                    CompanyId = companyId,
                    Action = action,
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