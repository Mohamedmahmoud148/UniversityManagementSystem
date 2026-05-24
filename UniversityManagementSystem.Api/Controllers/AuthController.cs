using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UniversityManagementSystem.Core.DTOs;
using UniversityManagementSystem.Core.Interfaces;
using UniversityManagementSystem.Infrastructure.Data;

namespace UniversityManagementSystem.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController(IAuthService authService, IUserContextService userContext, AppDbContext context) : ControllerBase
    {
        private readonly IAuthService _authService = authService;
        private readonly IUserContextService _userContext = userContext;
        private readonly AppDbContext _context = context;

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
        public async Task<IActionResult> GetMe()
        {
            var userId = _userContext.GetUserId();
            var user = await _context.SystemUsers
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null) return NotFound("User not found.");

            var role = User.Claims.FirstOrDefault(c => c.Type == "role")?.Value
                    ?? User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.Role)?.Value;

            object? profile = null;

            if (role == "Student")
            {
                var s = await _context.Students
                    .AsNoTracking()
                    .Include(x => x.Batch)
                    .Include(x => x.Group)
                    .Include(x => x.Department)
                    .Include(x => x.College)
                    .Include(x => x.Regulation)
                    .FirstOrDefaultAsync(x => x.SystemUserId == userId);

                if (s != null)
                    profile = new
                    {
                        profileId      = s.Id.ToString(),
                        code           = s.Code,
                        fullName       = s.FullName,
                        email          = s.Email,
                        phone          = s.Phone,
                        nationalId     = s.NationalId,
                        governorate    = s.Governorate,
                        address        = s.Address,
                        gender         = s.Gender?.ToString(),
                        dateOfBirth    = s.DateOfBirth,
                        studentType    = s.StudentType.ToString(),
                        religion       = s.Religion,
                        universityStudentId = s.UniversityStudentId,
                        isActive       = s.IsActive,
                        batchId        = s.BatchId.ToString(),
                        batchName      = s.Batch?.Name,
                        groupId        = s.GroupId.ToString(),
                        groupName      = s.Group?.Name,
                        departmentId   = s.DepartmentId.ToString(),
                        departmentName = s.Department?.Name,
                        collegeId      = s.CollegeId.ToString(),
                        collegeName    = s.College?.Name,
                        regulationId   = s.RegulationId?.ToString(),
                        regulationName = s.Regulation?.Title,
                    };
            }
            else if (role == "Doctor" || role == "TeachingAssistant")
            {
                var d = await _context.Doctors
                    .AsNoTracking()
                    .Include(x => x.Department)
                    .ThenInclude(x => x.College)
                    .FirstOrDefaultAsync(x => x.SystemUserId == userId);

                if (d != null)
                    profile = new
                    {
                        profileId          = d.Id.ToString(),
                        code               = d.Code,
                        fullName           = d.FullName,
                        email              = d.Email,
                        phone              = d.Phone,
                        universityStaffId  = d.UniversityStaffId,
                        departmentId       = d.DepartmentId.ToString(),
                        departmentName     = d.Department?.Name,
                        collegeName        = d.Department?.College?.Name,
                    };
            }
            else if (role == "Admin" || role == "SuperAdmin")
            {
                var a = await _context.Admins
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.SystemUserId == userId);

                if (a != null)
                    profile = new
                    {
                        profileId  = a.Id.ToString(),
                        code       = a.Code,
                        fullName   = a.FullName,
                        email      = a.Email,
                        phone      = a.Phone,
                    };
            }

            return Ok(new
            {
                userId             = user.Id.ToString(),
                fullName           = user.FullName,
                email              = user.Email,
                universityEmail    = user.UniversityEmail,
                nationalId         = user.NationalId,
                role,
                isActive           = user.IsActive,
                mustChangePassword = user.MustChangePassword,
                profile,
            });
        }

        [HttpPost("register/student")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<ActionResult<AuthResponseDto>> RegisterStudent(RegisterStudentDto dto)
        {
            var creatorId = _userContext.GetUserId();
            var result = await _authService.RegisterStudentAsync(dto, creatorId);
            return Ok(result);
        }

        [HttpPost("register/doctor")]
        [Authorize(Roles = "Admin,SuperAdmin")]
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
