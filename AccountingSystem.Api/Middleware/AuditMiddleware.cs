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
            // Only strictly log state-changing methods + Login
            var method = context.Request.Method;
            bool shouldLog = method == "POST" || method == "PUT" || method == "DELETE";

            if (!shouldLog)
            {
                await _next(context);
                return;
            }

            // --- 1. CAPTURE PHASE (Before Controller) ---
            string bodyContent = "";
            try
            {
                context.Request.EnableBuffering();
                using (var reader = new StreamReader(context.Request.Body, Encoding.UTF8, true, 1024, true))
                {
                    bodyContent = await reader.ReadToEndAsync();
                }
                context.Request.Body.Position = 0; // Rewind for the Controller
            }
            catch
            {
                // If reading fails, proceed but log error
                bodyContent = "[Error reading body]";
            }

            // Capture User Context *before* execution (in case context changes, though rare)
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

            // --- 2. EXECUTION PHASE ---
            await _next(context);

            // --- 3. VERIFICATION & LOGGING PHASE (After Controller) ---

            // Only log if the operation was SUCCESSFUL (200-299)
            if (context.Response.StatusCode >= 200 && context.Response.StatusCode < 300)
            {
                try
                {
                    // Logic to determine Action Name
                    string action = method;
                    string path = context.Request.Path.Value?.ToLower() ?? "";

                    if (path.Contains("/auth/login"))
                    {
                        action = "LOGIN";
                        bodyContent = "[Credentials Hidden]";
                    }
                    else if (path.Contains("/api/users"))
                    {
                        if (path.EndsWith("/restore")) action = "USER-RESTORE";
                        else if (action == "POST") action = "USER-CREATE";
                        else if (action == "DELETE") action = "USER-ARCHIVE";
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

                    // For Company Settings Update
                    else if (path.Contains("/companies/current") && action == "PUT")
                    {
                        action = "COMPANY-UPDATE";
                    }

                    // For Profile/Password Update
                    else if (path.Contains("/auth/profile")) action = "PROFILE-UPDATE";
                    else if (path.Contains("/auth/password")) action = "PASSWORD-CHANGE";

                    // SuperAdmin actions
                    else if (path.Contains("/superadmin/companies") && path.Contains("/status"))
                    {
                        action = "SUPERADMIN-COMPANY-STATUS";
                    }
                    else if (path.Contains("/superadmin/users") && path.Contains("/status"))
                    {
                        action = "SUPERADMIN-USER-STATUS";
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
                    // Fail silently so we don't break the response
                    Console.WriteLine($"Audit Logging Failed: {ex.Message}");
                }
            }
        }
    }
}