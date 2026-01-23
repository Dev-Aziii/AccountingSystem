using AccountingSystem.API.Data;
using AccountingSystem.API.Services.Interfaces;
using AccountingSystem.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AccountingSystem.API.Controllers
{
    [ApiController]
    [Route("api/users")]
    [Authorize(Roles = "Admin")]
    public class UsersController : ControllerBase
    {
        private readonly AccountingDbContext _context;
        private readonly IAuthService _authService;

        public UsersController(AccountingDbContext context, IAuthService authService)
        {
            _context = context;
            _authService = authService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAllUsers([FromQuery] bool includeArchived = false)
        {
            var query = _context.Users.Include(u => u.Role).AsQueryable();

            if (includeArchived)
            {
                query = query.IgnoreQueryFilters();
            }

            var users = await query
                .Select(u => new UserDTO
                {
                    Id = u.Id,
                    Email = u.Email,
                    FullName = u.FullName,
                    RoleName = u.Role.Name,
                    IsActive = u.IsActive,
                    IsDeleted = u.IsDeleted
                })
                .ToListAsync();

            return Ok(users);
        }

        [HttpPost]
        public async Task<IActionResult> CreateUser([FromBody] RegisterDTO registerDto)
        {
            try
            {
                var user = await _authService.RegisterAsync(registerDto);
                return Ok(new { message = "User created successfully", userId = user.Id });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteUser(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound("User not found");

            if (user.Email == "adzyl.jipos@gmail.com")
                return BadRequest(new { error = "Cannot delete the default System Administrator." });

            // SOFT DELETE
            user.IsDeleted = true;
            user.IsActive = false;

            await _context.SaveChangesAsync();
            return Ok(new { message = "User archived successfully" });
        }

        [HttpPut("{id}/restore")]
        public async Task<IActionResult> RestoreUser(int id)
        {
            var user = await _context.Users
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(u => u.Id == id);

            if (user == null) return NotFound("User not found");

            user.IsDeleted = false;
            user.IsActive = true;

            await _context.SaveChangesAsync();
            return Ok(new { message = "User restored successfully" });
        }
    }
}