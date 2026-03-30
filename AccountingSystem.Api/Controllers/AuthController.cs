using AccountingSystem.API.Configuration;
using AccountingSystem.API.Security;
using AccountingSystem.API.Services.Interfaces;
using AccountingSystem.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
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
        [EnableRateLimiting(AuthRateLimitPolicyNames.Login)]
        public async Task<IActionResult> Login([FromBody] LoginDTO loginDto)
        {
            try
            {
                var response = await _authService.LoginAsync(loginDto);
                return Ok(response);
            }
            catch (AuthFailureException ex)
            {
                if (ex.StatusCode == StatusCodes.Status401Unauthorized)
                {
                    return Unauthorized(new { error = ex.PublicMessage });
                }

                return StatusCode(ex.StatusCode, new { error = ex.PublicMessage });
            }
            catch (Exception)
            {
                return Unauthorized(new { error = AuthFailureException.DefaultPublicMessage });
            }
        }

        [HttpPost("login/mfa")]
        [EnableRateLimiting(AuthRateLimitPolicyNames.LoginMfa)]
        public async Task<IActionResult> LoginWithMfa([FromBody] LoginMfaDTO dto)
        {
            try
            {
                var response = await _authService.CompleteMfaLoginAsync(dto);
                return Ok(response);
            }
            catch (AuthFailureException ex)
            {
                if (ex.StatusCode == StatusCodes.Status401Unauthorized)
                {
                    return Unauthorized(new { error = ex.PublicMessage });
                }

                return StatusCode(ex.StatusCode, new { error = ex.PublicMessage });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPost("register-company")]
        [EnableRateLimiting(AuthRateLimitPolicyNames.RegisterCompany)]
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

        [HttpGet("profile")]
        [Authorize]
        public async Task<IActionResult> GetProfile()
        {
            try
            {
                var userId = GetCurrentUserId();
                var response = await _authService.GetCurrentProfileAsync(userId);
                return Ok(response);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

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

        [HttpPost("forgot-password")]
        [EnableRateLimiting(AuthRateLimitPolicyNames.ForgotPassword)]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDTO dto)
        {
            await _authService.SendPasswordResetAsync(dto);
            return Ok(new { message = "If the account exists, a password reset link has been sent." });
        }

        [HttpPost("confirm-email")]
        [EnableRateLimiting(AuthRateLimitPolicyNames.ConfirmEmail)]
        public async Task<IActionResult> ConfirmEmail([FromBody] ConfirmEmailDTO dto)
        {
            try
            {
                var result = await _authService.ConfirmEmailAsync(dto);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPost("resend-confirmation")]
        [EnableRateLimiting(AuthRateLimitPolicyNames.ResendConfirmation)]
        public async Task<IActionResult> ResendConfirmation([FromBody] ResendConfirmationDTO dto)
        {
            await _authService.ResendConfirmationAsync(dto);
            return Ok(new { message = "If the account exists and still needs confirmation, a new confirmation link has been sent." });
        }

        [HttpPost("reset-password")]
        [EnableRateLimiting(AuthRateLimitPolicyNames.ResetPassword)]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDTO dto)
        {
            try
            {
                await _authService.ResetPasswordAsync(dto);
                return Ok(new { message = "Password reset successfully." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPut("password")]
        [Authorize]
        [EnableRateLimiting(AuthRateLimitPolicyNames.ChangePassword)]
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

        [HttpGet("mfa")]
        [Authorize]
        [EnableRateLimiting(AuthRateLimitPolicyNames.MfaManage)]
        public async Task<IActionResult> GetMfaStatus()
        {
            try
            {
                var userId = GetCurrentUserId();
                var response = await _authService.GetMfaStatusAsync(userId);
                return Ok(response);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPost("mfa/authenticator/setup")]
        [Authorize]
        [EnableRateLimiting(AuthRateLimitPolicyNames.MfaManage)]
        public async Task<IActionResult> BeginAuthenticatorSetup()
        {
            try
            {
                var userId = GetCurrentUserId();
                var response = await _authService.BeginAuthenticatorSetupAsync(userId);
                return Ok(response);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPost("mfa/authenticator/reset")]
        [Authorize]
        [EnableRateLimiting(AuthRateLimitPolicyNames.MfaManage)]
        public async Task<IActionResult> ResetAuthenticator([FromBody] MfaReauthenticationDTO dto)
        {
            try
            {
                var userId = GetCurrentUserId();
                var response = await _authService.ResetAuthenticatorAsync(userId, dto);
                return Ok(response);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPost("mfa/authenticator/verify")]
        [Authorize]
        [EnableRateLimiting(AuthRateLimitPolicyNames.MfaManage)]
        public async Task<IActionResult> VerifyAuthenticatorSetup([FromBody] VerifyAuthenticatorSetupDTO dto)
        {
            try
            {
                var userId = GetCurrentUserId();
                var response = await _authService.VerifyAuthenticatorSetupAsync(userId, dto);
                return Ok(response);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPost("mfa/recovery-codes/regenerate")]
        [Authorize]
        [EnableRateLimiting(AuthRateLimitPolicyNames.MfaManage)]
        public async Task<IActionResult> RegenerateRecoveryCodes([FromBody] MfaReauthenticationDTO dto)
        {
            try
            {
                var userId = GetCurrentUserId();
                var response = await _authService.RegenerateRecoveryCodesAsync(userId, dto);
                return Ok(response);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPost("mfa/disable")]
        [Authorize]
        [EnableRateLimiting(AuthRateLimitPolicyNames.MfaManage)]
        public async Task<IActionResult> DisableMfa([FromBody] MfaReauthenticationDTO dto)
        {
            try
            {
                var userId = GetCurrentUserId();
                await _authService.DisableMfaAsync(userId, dto);
                return Ok(new { message = "Two-factor authentication has been disabled." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst("UserId");
            if (userIdClaim != null && int.TryParse(userIdClaim.Value, out var userId))
            {
                return userId;
            }

            throw new UnauthorizedAccessException("User ID not found in token.");
        }
    }
}
