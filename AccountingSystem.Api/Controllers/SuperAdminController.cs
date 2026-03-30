using AccountingSystem.API.Data;
using AccountingSystem.API.Identity;
using AccountingSystem.API.Models;
using AccountingSystem.API.Services.Interfaces;
using AccountingSystem.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AccountingSystem.API.Controllers
{
    [ApiController]
    [Route("api/superadmin")]
    [Authorize(Roles = "SuperAdmin")] // STRICTLY RESTRICTED
    public class SuperAdminController : ControllerBase
    {
        private readonly AccountingDbContext _context;
        private readonly ILogger<SuperAdminController> _logger;
        private readonly ILegacyIdentityBridgeService _identityBridgeService;

        public SuperAdminController(
            AccountingDbContext context,
            ILogger<SuperAdminController> logger,
            ILegacyIdentityBridgeService identityBridgeService)
        {
            _context = context;
            _logger = logger;
            _identityBridgeService = identityBridgeService;
        }

        private int GetCurrentUserId() => int.Parse(User.FindFirst("UserId")?.Value ?? "0");
        private string GetCurrentUserEmail() => User.FindFirst("unique_name")?.Value ?? User.Identity?.Name ?? "Unknown";

        private async Task<int> GetHostCompanyId()
        {
            var host = await _context.Companies.IgnoreQueryFilters().FirstOrDefaultAsync(c => c.Name == "SaaS Operations");
            return host?.Id ?? 0;
        }

        [HttpGet("dashboard")]
        public async Task<IActionResult> GetDashboardStats()
        {
            var hostCompanyId = await GetHostCompanyId();

            var companies = await _context.Companies
                .IgnoreQueryFilters()
                .Where(c => c.Id != hostCompanyId)
                .ToListAsync();

            var users = await _context.Users
                .IgnoreQueryFilters()
                .Include(u => u.Role)
                .Where(u => u.Role.Name != "SuperAdmin" && !u.IsDeleted)
                .ToListAsync();

            var now = DateTime.UtcNow;
            var twelveMonthsAgo = now.AddMonths(-11).Date;
            twelveMonthsAgo = new DateTime(twelveMonthsAgo.Year, twelveMonthsAgo.Month, 1);

            var monthlyRegistrations = companies
                .Where(c => c.CreatedAt >= twelveMonthsAgo)
                .GroupBy(c => new { c.CreatedAt.Year, c.CreatedAt.Month })
                .Select(g => new MonthlyActivityDTO
                {
                    Month = $"{g.Key.Year}-{g.Key.Month:D2}",
                    Count = g.Count()
                })
                .OrderBy(m => m.Month)
                .ToList();

            var allMonths = Enumerable.Range(0, 12).Select(i =>
            {
                var date = twelveMonthsAgo.AddMonths(i);
                return $"{date.Year}-{date.Month:D2}";
            }).ToList();

            monthlyRegistrations = allMonths.Select(m =>
                monthlyRegistrations.FirstOrDefault(r => r.Month == m) ?? new MonthlyActivityDTO { Month = m, Count = 0 }
            ).ToList();

            var monthlyUserGrowth = users
                .Where(u => u.CreatedAt >= twelveMonthsAgo)
                .GroupBy(u => new { u.CreatedAt.Year, u.CreatedAt.Month })
                .Select(g => new MonthlyActivityDTO
                {
                    Month = $"{g.Key.Year}-{g.Key.Month:D2}",
                    Count = g.Count()
                })
                .OrderBy(m => m.Month)
                .ToList();

            monthlyUserGrowth = allMonths.Select(m =>
                monthlyUserGrowth.FirstOrDefault(r => r.Month == m) ?? new MonthlyActivityDTO { Month = m, Count = 0 }
            ).ToList();

            _logger.LogInformation("Dashboard MonthlyRegistrations: {MonthlyRegistrations}",
                string.Join(", ", monthlyRegistrations.Select(m => $"{m.Month}:{m.Count}")));
            _logger.LogInformation("Dashboard MonthlyUserGrowth: {MonthlyUserGrowth}",
                string.Join(", ", monthlyUserGrowth.Select(m => $"{m.Month}:{m.Count}")));

            var recentActions = await _context.SuperAdminAuditLogs
                .OrderByDescending(l => l.Timestamp)
                .Take(10)
                .Select(l => new SuperAdminAuditLogDTO
                {
                    Id = l.Id,
                    AdminEmail = l.AdminEmail,
                    Action = l.Action,
                    TargetType = l.TargetType,
                    TargetName = l.TargetName,
                    TargetId = l.TargetId,
                    OldValue = l.OldValue,
                    NewValue = l.NewValue,
                    Details = l.Details,
                    Timestamp = l.Timestamp
                })
                .ToListAsync();

            var stats = new SystemDashboardDTO
            {
                TotalCompanies = companies.Count,
                ActiveCompanies = companies.Count(c => c.Status == "Active" && c.IsActive),
                SuspendedCompanies = companies.Count(c => c.Status == "Suspended" || (!c.IsActive && c.Status != "Blocked")),
                BlockedCompanies = companies.Count(c => c.Status == "Blocked"),
                TotalUsers = users.Count,
                ActiveUsers = users.Count(u => u.Status == "Active" && u.IsActive),
                RestrictedUsers = users.Count(u => u.Status == "Restricted"),
                BlockedUsers = users.Count(u => u.Status == "Blocked"),
                MonthlyRegistrations = monthlyRegistrations,
                MonthlyUserGrowth = monthlyUserGrowth,
                RecentActions = recentActions
            };

            return Ok(stats);
        }

        [HttpGet("companies")]
        public async Task<IActionResult> GetAllCompanies()
        {
            var hostCompanyId = await GetHostCompanyId();

            var companies = await _context.Companies
                .IgnoreQueryFilters()
                .Where(c => c.Id != hostCompanyId)
                .Select(c => new TenantDTO
                {
                    Id = c.Id,
                    Name = c.Name,
                    Address = c.Address,
                    CreatedAt = c.CreatedAt,
                    IsActive = c.IsActive,
                    Status = c.Status,
                    UserCount = _context.Users.IgnoreQueryFilters()
                        .Count(u => u.CompanyId == c.Id && u.Role.Name != "SuperAdmin" && !u.IsDeleted),
                    LastActivityDate = _context.AuditLogs.IgnoreQueryFilters()
                        .Where(a => a.CompanyId == c.Id)
                        .OrderByDescending(a => a.Timestamp)
                        .Select(a => (DateTime?)a.Timestamp)
                        .FirstOrDefault()
                })
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();

            return Ok(companies);
        }

        [HttpPut("companies/{id}/status")]
        public async Task<IActionResult> UpdateCompanyStatus(int id, [FromBody] UpdateCompanyStatusDTO dto)
        {
            var company = await _context.Companies.IgnoreQueryFilters().FirstOrDefaultAsync(c => c.Id == id);
            if (company == null) return NotFound("Company not found.");

            if (company.Name == "SaaS Operations")
                return BadRequest("Cannot modify the Host Operations company.");

            var validStatuses = new[] { "Active", "Suspended", "Blocked" };
            if (!validStatuses.Contains(dto.Status))
                return BadRequest("Invalid status. Must be: Active, Suspended, or Blocked.");

            var oldStatus = company.Status;
            company.Status = dto.Status;
            company.IsActive = dto.Status == "Active";

            await LogSuperAdminAction("COMPANY_STATUS_CHANGE", "Company", id, company.Name, oldStatus, dto.Status,
                $"Company '{company.Name}' status changed from {oldStatus} to {dto.Status}");

            await _context.SaveChangesAsync();

            return Ok(new { message = $"Company '{company.Name}' is now {dto.Status}." });
        }

        [HttpPut("companies/{id}/toggle")]
        public async Task<IActionResult> ToggleCompanyStatus(int id)
        {
            var company = await _context.Companies.IgnoreQueryFilters().FirstOrDefaultAsync(c => c.Id == id);
            if (company == null) return NotFound("Company not found.");

            if (company.Name == "SaaS Operations")
                return BadRequest("Cannot suspend the Host Operations company.");

            var oldStatus = company.Status;
            company.IsActive = !company.IsActive;
            company.Status = company.IsActive ? "Active" : "Suspended";

            await LogSuperAdminAction("COMPANY_STATUS_CHANGE", "Company", id, company.Name, oldStatus, company.Status,
                $"Company '{company.Name}' status toggled from {oldStatus} to {company.Status}");

            await _context.SaveChangesAsync();

            return Ok(new { message = company.IsActive ? "Company activated." : "Company suspended." });
        }

        [HttpGet("users")]
        public async Task<IActionResult> GetAllUsers()
        {
            var users = await _context.Users
                .IgnoreQueryFilters()
                .Include(u => u.Role)
                .Where(u => u.Role.Name != "SuperAdmin" && !u.IsDeleted)
                .Join(_context.Companies.IgnoreQueryFilters(),
                    user => user.CompanyId,
                    company => company.Id,
                    (user, company) => new GlobalUserDTO
                    {
                        Id = user.Id,
                        FullName = user.FullName,
                        Email = user.Email,
                        Role = user.Role.Name,
                        CompanyName = company.Name,
                        CompanyId = company.Id,
                        IsActive = user.IsActive,
                        Status = user.Status,
                        CreatedAt = user.CreatedAt
                    })
                .OrderBy(u => u.CompanyName)
                .ThenBy(u => u.FullName)
                .ToListAsync();

            return Ok(users);
        }

        [HttpPut("users/{id}/status")]
        public async Task<IActionResult> UpdateUserStatus(int id, [FromBody] UpdateUserStatusDTO dto)
        {
            var user = await _context.Users.IgnoreQueryFilters()
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.Id == id);
            if (user == null) return NotFound("User not found.");

            if (user.Role.Name == "SuperAdmin")
                return BadRequest("Cannot modify SuperAdmin accounts.");

            var currentUserId = GetCurrentUserId();
            if (user.Id == currentUserId)
                return BadRequest("You cannot modify your own account status.");

            var validStatuses = new[] { "Active", "Restricted", "Blocked" };
            if (!validStatuses.Contains(dto.Status))
                return BadRequest("Invalid status. Must be: Active, Restricted, or Blocked.");

            var oldStatus = user.Status;
            user.Status = dto.Status;
            user.IsActive = dto.Status == "Active";

            await LogSuperAdminAction("USER_STATUS_CHANGE", "User", id, $"{user.FullName} ({user.Email})", oldStatus, dto.Status,
                $"User '{user.Email}' status changed from {oldStatus} to {dto.Status}");

            await _context.SaveChangesAsync();
            await _identityBridgeService.SyncExistingUserStatusAsync(CreateIdentitySnapshot(user));

            return Ok(new { message = $"User '{user.Email}' is now {dto.Status}." });
        }

        [HttpPut("users/{id}/toggle")]
        public async Task<IActionResult> ToggleUserStatus(int id)
        {
            var user = await _context.Users.IgnoreQueryFilters()
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.Id == id);
            if (user == null) return NotFound("User not found.");

            if (user.Role.Name == "SuperAdmin")
                return BadRequest("Cannot modify SuperAdmin accounts.");

            var currentUserId = GetCurrentUserId();
            if (user.Id == currentUserId)
                return BadRequest("You cannot suspend your own account.");

            var oldStatus = user.Status;
            user.IsActive = !user.IsActive;
            user.Status = user.IsActive ? "Active" : "Blocked";

            await LogSuperAdminAction("USER_STATUS_CHANGE", "User", id, $"{user.FullName} ({user.Email})", oldStatus, user.Status,
                $"User '{user.Email}' status toggled from {oldStatus} to {user.Status}");

            await _context.SaveChangesAsync();
            await _identityBridgeService.SyncExistingUserStatusAsync(CreateIdentitySnapshot(user));

            return Ok(new { message = user.IsActive ? "User activated." : "User blocked." });
        }

        [HttpGet("audit-logs")]
        public async Task<IActionResult> GetSuperAdminAuditLogs()
        {
            var logs = await _context.SuperAdminAuditLogs
                .OrderByDescending(l => l.Timestamp)
                .Take(500)
                .Select(l => new SuperAdminAuditLogDTO
                {
                    Id = l.Id,
                    AdminEmail = l.AdminEmail,
                    Action = l.Action,
                    TargetType = l.TargetType,
                    TargetName = l.TargetName,
                    TargetId = l.TargetId,
                    OldValue = l.OldValue,
                    NewValue = l.NewValue,
                    Details = l.Details,
                    Timestamp = l.Timestamp
                })
                .ToListAsync();

            return Ok(logs);
        }

        private Task LogSuperAdminAction(string action, string targetType, int targetId, string targetName,
            string oldValue, string newValue, string details)
        {
            var log = new SuperAdminAuditLog
            {
                AdminUserId = GetCurrentUserId(),
                AdminEmail = GetCurrentUserEmail(),
                Action = action,
                TargetType = targetType,
                TargetId = targetId,
                TargetName = targetName,
                OldValue = oldValue,
                NewValue = newValue,
                Details = details,
                Timestamp = DateTime.UtcNow
            };

            _context.SuperAdminAuditLogs.Add(log);
            return Task.CompletedTask;
        }

        private static LegacyIdentityUserSnapshot CreateIdentitySnapshot(User user) =>
            new(
                user.Id,
                user.CompanyId,
                user.Email,
                user.FullName ?? user.Email,
                user.Status,
                user.IsActive,
                user.IsDeleted,
                user.Role.Name);
    }
}
