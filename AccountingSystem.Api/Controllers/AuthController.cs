using AccountingSystem.API.Services.Interfaces;
using AccountingSystem.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace AccountingSystem.API.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDTO loginDto)
        {
            try
            {
                var response = await _authService.LoginAsync(loginDto);
                return Ok(response);
            }
            catch (Exception ex)
            {
                return Unauthorized(new { error = ex.Message });
            }
        }

        [HttpPost("register-company")]
        public async Task<IActionResult> RegisterCompany([FromBody] CompanyRegisterDTO dto)
        {
            try
            {
                var response = await _authService.RegisterCompanyAsync(dto);
                return Ok(response);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        // Profile & Password Endpoints ---

        [HttpPut("profile")]
        [Authorize]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileDTO dto)
        {
            try
            {
                var userId = GetCurrentUserId();
                await _authService.UpdateProfileAsync(userId, dto);
                return Ok(new { message = "Profile updated successfully." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPut("password")]
        [Authorize]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDTO dto)
        {
            try
            {
                var userId = GetCurrentUserId();
                await _authService.ChangePasswordAsync(userId, dto);
                return Ok(new { message = "Password changed successfully." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        // Helper to extract UserId safely
        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst("UserId");
            if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
            {
                return userId;
            }
            throw new UnauthorizedAccessException("User ID not found in token.");
        }
    }
}