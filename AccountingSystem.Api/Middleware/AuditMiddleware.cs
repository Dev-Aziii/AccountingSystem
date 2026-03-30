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
            var method = context.Request.Method;
            var path = context.Request.Path.Value?.ToLowerInvariant() ?? string.Empty;
            var shouldLog = method == "POST" || method == "PUT" || method == "DELETE";

            if (!shouldLog || path.StartsWith("/api/auth/") || path.StartsWith("/api/superadmin/"))
            {
                await _next(context);
                return;
            }

            var bodyContent = string.Empty;
            try
            {
                context.Request.EnableBuffering();
                using (var reader = new StreamReader(context.Request.Body, Encoding.UTF8, true, 1024, true))
                {
                    bodyContent = await reader.ReadToEndAsync();
                }

                context.Request.Body.Position = 0;
            }
            catch
            {
                bodyContent = "[Error reading body]";
            }

            int? userId = null;
            if (context.Items["UserId"] is string userIdStr && int.TryParse(userIdStr, out var parsedUserId))
            {
                userId = parsedUserId;
            }

            var companyId = 0;
            if (context.Items["CompanyId"] is string companyIdStr && int.TryParse(companyIdStr, out var parsedCompanyId))
            {
                companyId = parsedCompanyId;
            }

            var remoteIpAddress = context.Connection.RemoteIpAddress?.ToString();

            await _next(context);

            if (context.Response.StatusCode < 200 || context.Response.StatusCode >= 300)
            {
                return;
            }

            try
            {
                var action = method;

                if (path.Contains("/api/users"))
                {
                    if (path.EndsWith("/restore"))
                    {
                        action = "USER-RESTORE";
                    }
                    else if (action == "POST")
                    {
                        action = "USER-CREATE";
                        bodyContent = "[Sensitive user creation payload hidden]";
                    }
                    else if (action == "DELETE")
                    {
                        action = "USER-ARCHIVE";
                    }
                }
                else if (path.Contains("/receivables/customers"))
                {
                    if (path.EndsWith("/restore")) action = "CUSTOMER-RESTORE";
                    else if (action == "POST") action = "CUSTOMER-CREATE";
                    else if (action == "PUT") action = "CUSTOMER-UPDATE";
                    else if (action == "DELETE") action = "CUSTOMER-ARCHIVE";
                }
                else if (path.Contains("/payables/vendors"))
                {
                    if (path.EndsWith("/restore")) action = "VENDOR-RESTORE";
                    else if (action == "POST") action = "VENDOR-CREATE";
                    else if (action == "PUT") action = "VENDOR-UPDATE";
                    else if (action == "DELETE") action = "VENDOR-ARCHIVE";
                }
                else if (path.Contains("/ledger/accounts"))
                {
                    if (path.EndsWith("/restore")) action = "ACCOUNT-RESTORE";
                    else if (action == "POST") action = "ACCOUNT-CREATE";
                    else if (action == "PUT") action = "ACCOUNT-UPDATE";
                    else if (action == "DELETE") action = "ACCOUNT-ARCHIVE";
                }
                else if (path.Contains("/bill") && action == "POST")
                {
                    action = path.Contains("/pay") ? "BILL-PAY" : "BILL-CREATE";
                }
                else if (path.Contains("/invoice") && action == "POST")
                {
                    action = path.Contains("/receive") ? "INVOICE-PAYMENT" : "INVOICE-CREATE";
                }
                else if (path.Contains("/journal") && action == "POST")
                {
                    action = "JOURNAL-ENTRY";
                }
                else if (path.Contains("/companies/current") && action == "PUT")
                {
                    action = "COMPANY-UPDATE";
                }
                var auditLog = new AuditLog
                {
                    UserId = userId,
                    CompanyId = companyId,
                    Action = action,
                    EntityName = context.Request.Path,
                    EntityId = "N/A",
                    IpAddress = remoteIpAddress,
                    Timestamp = DateTime.UtcNow,
                    Changes = bodyContent.Length > 2000 ? bodyContent[..2000] : bodyContent
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
