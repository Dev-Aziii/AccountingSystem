using AccountingSystem.API.Data;
using AccountingSystem.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace AccountingSystem.API.Controllers
{
    [ApiController]
    [Route("api/audit-logs")]
    [Authorize(Policy = ApplicationAuthorizationPolicies.RequireTenantOwner)]
    public class AuditLogsController : ControllerBase
    {
        private readonly AccountingDbContext _context;

        public AuditLogsController(AccountingDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetAuditLogs()
        {
            var logRows = await (from log in _context.AuditLogs
                                 join user in _context.Users.IgnoreQueryFilters() on log.UserId equals user.Id into userJoin
                                 from u in userJoin.DefaultIfEmpty()
                                 where !EF.Functions.Like(log.Action, "SUPERADMIN-%")
                                 where !EF.Functions.Like(log.Action, "AUTH-%") ||
                                       (
                                           (u == null || u.Role.Name != ApplicationRoles.SuperAdmin) &&
                                           !EF.Functions.Like(log.EntityName ?? string.Empty, "/api/superadmin/%") &&
                                           !EF.Functions.Like(log.Changes ?? string.Empty, "%\"reason\":\"SuperAdminRole\"%")
                                       )
                                 orderby log.Timestamp descending
                                 select new
                                 {
                                     log.Id,
                                     log.UserId,
                                     ResolvedUserEmail = u != null ? u.Email : null,
                                     ResolvedUserRole = u != null ? u.Role.Name : null,
                                     log.Action,
                                     log.EntityName,
                                     log.EntityId,
                                     log.IpAddress,
                                     log.Timestamp,
                                     log.Changes
                                 })
                .Take(500)
                .ToListAsync();

            var logs = logRows
                .Where(log => !IsPlatformOriginated(log.Action, log.ResolvedUserRole, log.EntityName, log.Changes))
                .Select(log => new AuditLogDTO
            {
                Id = log.Id,
                UserEmail = log.ResolvedUserEmail
                    ?? ExtractStringFromChanges(log.Changes, "email")
                    ?? (log.UserId.HasValue ? $"User #{log.UserId}" : "System/Anonymous"),
                Action = log.Action,
                EntityName = log.EntityName,
                EntityId = log.EntityId,
                IpAddress = ResolveIpAddress(log.IpAddress, log.Changes),
                Timestamp = log.Timestamp,
                Changes = log.Changes
            })
                .Take(500)
                .ToList();

            return Ok(logs);
        }

        private static bool IsPlatformOriginated(string action, string? resolvedUserRole, string? entityName, string? changes)
        {
            if (action.StartsWith("SUPERADMIN-", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (!action.StartsWith("AUTH-", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return string.Equals(resolvedUserRole, ApplicationRoles.SuperAdmin, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(ExtractStringFromChanges(changes, "reason"), "SuperAdminRole", StringComparison.OrdinalIgnoreCase) ||
                   (!string.IsNullOrWhiteSpace(entityName) &&
                    entityName.StartsWith("/api/superadmin/", StringComparison.OrdinalIgnoreCase));
        }

        private static string? ResolveIpAddress(string? ipAddress, string? changes) =>
            !string.IsNullOrWhiteSpace(ipAddress)
                ? ipAddress
                : ExtractStringFromChanges(changes, "remoteIpAddress");

        private static string? ExtractStringFromChanges(string? changes, string propertyName)
        {
            if (string.IsNullOrWhiteSpace(changes))
            {
                return null;
            }

            try
            {
                using var document = JsonDocument.Parse(changes);
                return document.RootElement.TryGetProperty(propertyName, out var property) &&
                       property.ValueKind == JsonValueKind.String
                    ? property.GetString()
                    : null;
            }
            catch (JsonException)
            {
                return null;
            }
        }
    }
}
