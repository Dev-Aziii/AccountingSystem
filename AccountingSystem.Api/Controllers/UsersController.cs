using AccountingSystem.API.Data;
using AccountingSystem.API.Identity;
using AccountingSystem.API.Security;
using AccountingSystem.API.Services.Interfaces;
using AccountingSystem.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AccountingSystem.API.Controllers
{
    [ApiController]
    [Route("api/users")]
    [Authorize(Policy = ApplicationAuthorizationPolicies.RequireTenantOwner)]
    public class UsersController : ControllerBase
    {
        private readonly AccountingDbContext _context;
        private readonly IAuthService _authService;
        private readonly ILegacyIdentityBridgeService _identityBridgeService;

        public UsersController(
            AccountingDbContext context,
            IAuthService authService,
            ILegacyIdentityBridgeService identityBridgeService)
        {
            _context = context;
            _authService = authService;
            _identityBridgeService = identityBridgeService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAllUsers([FromQuery] bool includeArchived = false)
        {
            if (!RoleAssignmentValidator.TryGetTenantOwnerScope(User, out var tenantId))
            {
                return Forbid();
            }

            var query = includeArchived
                ? _context.Users.IgnoreQueryFilters().Include(u => u.Role).Where(u => u.CompanyId == tenantId)
                : _context.Users.Include(u => u.Role).Where(u => u.CompanyId == tenantId);

            var users = await query
                .Select(u => new UserDTO
                {
                    Id = u.Id,
                    Email = u.Email,
                    FullName = u.FullName,
                    RoleName = u.Role.Name,
                    Status = u.Status,
                    IsActive = u.IsActive,
                    IsDeleted = u.IsDeleted
                })
                .ToListAsync();

            return Ok(users);
        }

        [HttpPost]
        public async Task<IActionResult> CreateUser([FromBody] InviteTenantUserDTO dto)
        {
            if (!RoleAssignmentValidator.TryGetTenantOwnerScope(User, out _))
            {
                return Forbid();
            }

            try
            {
                var user = await _authService.InviteTenantUserAsync(dto);
                return Ok(new { message = "Invite sent successfully", userId = user.Id });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPost("{id}/resend-invite")]
        public async Task<IActionResult> ResendInvite(int id)
        {
            if (!RoleAssignmentValidator.TryGetTenantOwnerScope(User, out var tenantId))
            {
                return Forbid();
            }

            try
            {
                await _authService.ResendInviteAsync(id, tenantId);
                return Ok(new { message = "Invite sent successfully." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteUser(int id)
        {
            if (!RoleAssignmentValidator.TryGetTenantOwnerScope(User, out var tenantId))
            {
                return Forbid();
            }

            var user = await _context.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.Id == id && u.CompanyId == tenantId);
            if (user == null) return NotFound("User not found");

            try
            {
                RoleAssignmentValidator.EnsureTenantManagedUser(user);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }

            user.IsDeleted = true;
            user.IsActive = false;

            await _context.SaveChangesAsync();
            await _identityBridgeService.SyncExistingUserStatusAsync(CreateIdentitySnapshot(user));
            return Ok(new { message = "User archived successfully" });
        }

        [HttpPut("{id}/restore")]
        public async Task<IActionResult> RestoreUser(int id)
        {
            if (!RoleAssignmentValidator.TryGetTenantOwnerScope(User, out var tenantId))
            {
                return Forbid();
            }

            var user = await _context.Users
                .IgnoreQueryFilters()
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.Id == id && u.CompanyId == tenantId);

            if (user == null) return NotFound("User not found");

            try
            {
                RoleAssignmentValidator.EnsureTenantManagedUser(user);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }

            user.IsDeleted = false;
            user.IsActive = string.Equals(user.Status, ApplicationUserStatuses.Active, StringComparison.Ordinal);

            await _context.SaveChangesAsync();
            await _identityBridgeService.SyncExistingUserStatusAsync(CreateIdentitySnapshot(user));
            return Ok(new { message = "User restored successfully" });
        }

        private static LegacyIdentityUserSnapshot CreateIdentitySnapshot(API.Models.User user) =>
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
