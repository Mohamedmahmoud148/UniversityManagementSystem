using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniversityManagementSystem.Core.DTOs;
using UniversityManagementSystem.Core.Interfaces;

namespace UniversityManagementSystem.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController(IAuthService authService, IUserContextService userContext) : ControllerBase
    {
        private readonly IAuthService _authService = authService;
        private readonly IUserContextService _userContext = userContext;

        [AllowAnonymous]
        [HttpPost("login")]
        [EnableRateLimiting("LoginPolicy")]
        public async Task<ActionResult<AuthResponseDto>> Login(UserLoginDto loginDto)
        {
            var result = await _authService.LoginAsync(loginDto);
            if (result == null) return Unauthorized("Invalid credentials or account locked.");
            return Ok(result);
        }

        [HttpPost("refresh-token")]
        [AllowAnonymous]
        [EnableRateLimiting("SensitiveAuthPolicy")]
        public async Task<ActionResult<AuthResponseDto>> RefreshToken(RefreshTokenRequestDto dto)
        {
            var result = await _authService.RefreshTokenAsync(dto.Token, dto.RefreshToken);
            return Ok(result);
        }

        [HttpPost("revoke-token")]
        [Authorize]
        public async Task<IActionResult> RevokeToken(RevokeTokenRequestDto dto)
        {
            var result = await _authService.RevokeTokenAsync(dto.RefreshToken);
            if (!result) return NotFound("Token not found.");
            return Ok("Token revoked.");
        }

        [HttpPost("change-password")]
        [Authorize]
        [EnableRateLimiting("SensitiveAuthPolicy")]
        public async Task<IActionResult> ChangePassword(ChangePasswordDto dto)
        {
            var userId = _userContext.GetUserId();
            var result = await _authService.ChangePasswordAsync(userId, dto.CurrentPassword, dto.NewPassword);
            if (!result) return BadRequest("Password change failed.");
            return Ok("Password changed successfully.");
        }

        [HttpPost("logout")]
        [Authorize]
        public async Task<IActionResult> Logout(RevokeTokenRequestDto dto)
        {
            await _authService.RevokeTokenAsync(dto.RefreshToken);
            return Ok("Logged out.");
        }

        [HttpGet("me")]
        [Authorize]
        public IActionResult GetMe()
        {
            return Ok(User.Claims.Select(c => new { c.Type, c.Value }));
        }

        [HttpPost("register/student")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<AuthResponseDto>> RegisterStudent(RegisterStudentDto dto)
        {
            var creatorId = _userContext.GetUserId();
            var result = await _authService.RegisterStudentAsync(dto, creatorId);
            return Ok(result);
        }

        [HttpPost("register/doctor")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<AuthResponseDto>> RegisterDoctor(RegisterDoctorDto dto)
        {
            var creatorId = _userContext.GetUserId();
            var result = await _authService.RegisterDoctorAsync(dto, creatorId);
            return Ok(result);
        }

        [HttpPost("register/admin")]
        [Authorize(Roles = "SuperAdmin")]
        public async Task<ActionResult<AuthResponseDto>> RegisterAdmin(RegisterAdminDto dto)
        {
            var creatorId = _userContext.GetUserId();
            var result = await _authService.RegisterAdminAsync(dto, creatorId);
            return Ok(result);
        }

        // ── POST /api/auth/admin/reset-password/{userId} ──────────────────────
        /// <summary>
        /// Admin resets any user's password without needing the old one.
        /// Returns the new temporary password — share it with the user.
        /// The user will be forced to change it on next login (MustChangePassword=true).
        /// </summary>
        [HttpPost("admin/reset-password/{userId}")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<IActionResult> ResetPassword(string userId)
        {
            if (!NUlid.Ulid.TryParse(userId, out var uid))
                return BadRequest("Invalid user ID format.");

            try
            {
                var newPassword = await _authService.ResetPasswordAsync(uid);
                return Ok(new
                {
                    Message         = "Password reset successfully.",
                    NewPassword     = newPassword,
                    MustChangePassword = true,
                    Note            = "Share this password with the user. They will be required to change it on first login."
                });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
        }
    }
}
