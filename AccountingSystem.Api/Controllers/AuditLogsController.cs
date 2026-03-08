using AccountingSystem.API.Data;
using AccountingSystem.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AccountingSystem.API.Controllers
{
    [ApiController]
    [Route("api/audit-logs")]
    [Authorize(Roles = "Admin")] 
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
            // Join AuditLogs with Users to resolve the Email
            var query = from log in _context.AuditLogs
                        join user in _context.Users on log.UserId equals user.Id into userJoin
                        from u in userJoin.DefaultIfEmpty()
                        orderby log.Timestamp descending
                        select new AuditLogDTO
                        {
                            Id = log.Id,
                            UserEmail = u != null ? u.Email : (log.UserId.HasValue ? $"User #{log.UserId}" : "System/Anonymous"),
                            Action = log.Action,
                            EntityName = log.EntityName,
                            EntityId = log.EntityId,
                            Timestamp = log.Timestamp,
                            Changes = log.Changes
                        };

            // Limit to last 500 records for performance
            var logs = await query.Take(500).ToListAsync();

            return Ok(logs);
        }
    }
}