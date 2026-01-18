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
    [Authorize(Roles = "Admin")] // STRICTLY ADMIN ONLY
    public class UsersController : ControllerBase
    {
        private readonly AccountingDbContext _context;
        private readonly IAuthService _authService;

        public UsersController(AccountingDbContext context, IAuthService authService)
        {
            _context = context;
            _authService = authService;
        }

        // GET: api/users
        [HttpGet]
        public async Task<IActionResult> GetAllUsers()
        {
            var users = await _context.Users
                .Include(u => u.Role)
                .Select(u => new UserDTO
                {
                    Id = u.Id,
                    Username = u.Username,
                    FullName = u.FullName,
                    RoleName = u.Role.Name
                })
                .ToListAsync();

            return Ok(users);
        }

        // POST: api/users (Create new user)
        // We reuse the existing RegisterDTO and Logic from AuthService
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

        // DELETE: api/users/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteUser(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound("User not found");

            // Prevent deleting the last Admin (Self-preservation check could be added here)
            if (user.Username == "admin")
                return BadRequest(new { error = "Cannot delete the default System Administrator." });

            _context.Users.Remove(user);
            await _context.SaveChangesAsync();
            return Ok(new { message = "User deleted successfully" });
        }
    }
}