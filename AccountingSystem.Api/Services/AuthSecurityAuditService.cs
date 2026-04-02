using AccountingSystem.API.Data;
using AccountingSystem.API.Models;
using AccountingSystem.API.Services.Interfaces;
using System.Text.Json;

namespace AccountingSystem.API.Services
{
    public class AuthSecurityAuditService : IAuthSecurityAuditService
    {
        private readonly AccountingDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<AuthSecurityAuditService> _logger;

        public AuthSecurityAuditService(
            AccountingDbContext context,
            IHttpContextAccessor httpContextAccessor,
            ILogger<AuthSecurityAuditService> logger)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
        }

        public async Task WriteAsync(
            string action,
            int? userId = null,
            int? companyId = null,
            string? email = null,
            string? reason = null,
            int? failedAttempts = null,
            DateTime? lockoutEndUtc = null,
            string? policy = null,
            int? lockoutDurationMinutes = null,
            int? retryAfterSeconds = null,
            string? disabledReason = null)
        {
            try
            {
                var httpContext = _httpContextAccessor.HttpContext;
                var path = httpContext?.Request.Path.Value ?? "N/A";
                var remoteIpAddress = httpContext?.Connection.RemoteIpAddress?.ToString();
                var resolvedUserId = userId ?? TryParseClaim(httpContext, "UserId");
                var resolvedCompanyId = companyId ?? TryParseClaim(httpContext, "CompanyId") ?? 0;
                var resolvedEmail = email ?? httpContext?.User?.Identity?.Name;

                var metadata = new
                {
                    path,
                    remoteIpAddress,
                    email = resolvedEmail,
                    reason,
                    failedAttempts,
                    lockoutEndUtc,
                    policy,
                    lockoutDurationMinutes,
                    retryAfterSeconds,
                    disabledReason
                };

                var auditLog = new AuditLog
                {
                    UserId = resolvedUserId,
                    CompanyId = resolvedCompanyId,
                    Action = action,
                    EntityName = path,
                    EntityId = "N/A",
                    IpAddress = remoteIpAddress,
                    Timestamp = DateTime.UtcNow,
                    Changes = SerializeAndTrim(metadata)
                };

                _context.AuditLogs.Add(auditLog);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to write auth security audit event {Action}.", action);
            }
        }

        private static int? TryParseClaim(HttpContext? httpContext, string claimType)
        {
            var value = httpContext?.User?.FindFirst(claimType)?.Value;
            return int.TryParse(value, out var parsedValue) ? parsedValue : null;
        }

        private static string SerializeAndTrim(object metadata)
        {
            var serialized = JsonSerializer.Serialize(metadata);
            return serialized.Length > 2000 ? serialized[..2000] : serialized;
        }
    }
}
