using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NUlid;
using System.Security.Claims;
using UniversityManagementSystem.Core.DTOs;
using UniversityManagementSystem.Core.Interfaces;

namespace UniversityManagementSystem.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController(IAuthService authService) : ControllerBase
    {
        private readonly IAuthService _authService = authService;

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
        public async Task<IActionResult> ChangePassword(ChangePasswordDto dto)
        {
            var claim = User.FindFirst("UserId")?.Value;
            if (!Ulid.TryParse(claim, out var userId)) return Unauthorized("Invalid UserId in token.");
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
            var claim = User.FindFirst("UserId")?.Value;
            if (!Ulid.TryParse(claim, out var creatorId)) return Unauthorized("Invalid creator ID.");
            var result = await _authService.RegisterStudentAsync(dto, creatorId);
            return Ok(result);
        }

        [HttpPost("register/doctor")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<AuthResponseDto>> RegisterDoctor(RegisterDoctorDto dto)
        {
            var claim = User.FindFirst("UserId")?.Value;
            if (!Ulid.TryParse(claim, out var creatorId)) return Unauthorized("Invalid creator ID.");
            var result = await _authService.RegisterDoctorAsync(dto, creatorId);
            return Ok(result);
        }

        [HttpPost("register/admin")]
        [Authorize(Roles = "SuperAdmin")]
        public async Task<ActionResult<AuthResponseDto>> RegisterAdmin(RegisterAdminDto dto)
        {
            var claim = User.FindFirst("nameid")?.Value;
            if (!Ulid.TryParse(claim, out var creatorId)) return Unauthorized("Invalid creator ID.");
            var result = await _authService.RegisterAdminAsync(dto, creatorId);
            return Ok(result);
        }
    }
}
