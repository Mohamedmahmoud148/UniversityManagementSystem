using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UniversityManagementSystem.Core.Constants;
using UniversityManagementSystem.Core.DTOs;
using UniversityManagementSystem.Core.Entities;
using UniversityManagementSystem.Core.Interfaces;
using UniversityManagementSystem.Infrastructure.Data;

namespace UniversityManagementSystem.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController(
        IAuthService authService,
        IUserContextService userContext,
        IAuditService auditService,
        AppDbContext context) : ControllerBase
    {
        private readonly IAuthService _authService = authService;
        private readonly IUserContextService _userContext = userContext;
        private readonly IAuditService _auditService = auditService;
        private readonly AppDbContext _context = context;

        [AllowAnonymous]
        [HttpPost("login")]
        [EnableRateLimiting("LoginPolicy")]
        public async Task<ActionResult<AuthResponseDto>> Login(UserLoginDto loginDto)
        {
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            var ua = Request.Headers["User-Agent"].ToString();

            try
            {
                var result = await _authService.LoginAsync(loginDto);
                if (result == null)
                {
                    await _auditService.LogAsync(new AuditLogEntry
                    {
                        Action      = AuditActions.FailedLogin,
                        Entity      = "SystemUser",
                        EntityId    = loginDto.Email,
                        Description = $"Failed login attempt for {loginDto.Email}",
                        Severity    = AuditSeverity.Warning,
                        Status      = "Failed",
                        Email       = loginDto.Email,
                        IpAddress   = ip,
                        UserAgent   = ua,
                        Browser     = ParseBrowser(ua),
                        Device      = ParseDevice(ua)
                    });
                    return Unauthorized("Invalid credentials or account locked.");
                }

                // Fetch user info for the audit log
                var user = await _context.SystemUsers.AsNoTracking()
                    .FirstOrDefaultAsync(u => u.Email == loginDto.Email.Trim()
                                           || u.UniversityEmail == loginDto.Email.Trim());

                await _auditService.LogAsync(new AuditLogEntry
                {
                    Action      = AuditActions.Login,
                    Entity      = "SystemUser",
                    EntityId    = user?.Id.ToString() ?? loginDto.Email,
                    Description = $"{user?.FullName ?? loginDto.Email} logged in",
                    Severity    = AuditSeverity.Info,
                    Status      = "Success",
                    UserId      = user?.Id,
                    UserName    = user?.FullName,
                    Email       = user?.Email ?? loginDto.Email,
                    Role        = user?.Role.ToString(),
                    IpAddress   = ip,
                    UserAgent   = ua,
                    Browser     = ParseBrowser(ua),
                    Device      = ParseDevice(ua)
                });

                return Ok(result);
            }
            catch (Exception ex)
            {
                await _auditService.LogAsync(new AuditLogEntry
                {
                    Action      = AuditActions.FailedLogin,
                    Entity      = "SystemUser",
                    EntityId    = loginDto.Email,
                    Description = $"Login error for {loginDto.Email}: {ex.Message}",
                    Severity    = AuditSeverity.Warning,
                    Status      = "Failed",
                    Email       = loginDto.Email,
                    IpAddress   = ip,
                    UserAgent   = ua,
                    Browser     = ParseBrowser(ua),
                    Device      = ParseDevice(ua)
                });
                throw;
            }
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

            await _auditService.LogAsync(new AuditLogEntry
            {
                Action      = AuditActions.ChangePassword,
                Entity      = "SystemUser",
                EntityId    = userId.ToString(),
                Description = "Password changed",
                Severity    = AuditSeverity.Info,
                Status      = "Success",
                UserId      = userId,
                IpAddress   = HttpContext.Connection.RemoteIpAddress?.ToString()
            });

            return Ok("Password changed successfully.");
        }

        [HttpPost("logout")]
        [Authorize]
        public async Task<IActionResult> Logout(RevokeTokenRequestDto dto)
        {
            var userId = _userContext.GetUserId();
            await _authService.RevokeTokenAsync(dto.RefreshToken);

            var user = await _context.SystemUsers.AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == userId);

            var ua = Request.Headers["User-Agent"].ToString();

            await _auditService.LogAsync(new AuditLogEntry
            {
                Action      = AuditActions.Logout,
                Entity      = "SystemUser",
                EntityId    = userId.ToString(),
                Description = $"{user?.FullName ?? userId.ToString()} logged out",
                Severity    = AuditSeverity.Info,
                Status      = "Success",
                UserId      = userId,
                UserName    = user?.FullName,
                Email       = user?.Email,
                Role        = user?.Role.ToString(),
                IpAddress   = HttpContext.Connection.RemoteIpAddress?.ToString(),
                UserAgent   = ua,
                Browser     = ParseBrowser(ua),
                Device      = ParseDevice(ua)
            });

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

        // ── Helpers ───────────────────────────────────────────────────────────
        private static string ParseBrowser(string ua) =>
            ua.Contains("Edg")     ? "Edge"    :
            ua.Contains("Chrome")  ? "Chrome"  :
            ua.Contains("Firefox") ? "Firefox" :
            ua.Contains("Safari")  ? "Safari"  : "Unknown";

        private static string ParseDevice(string ua) =>
            ua.Contains("Mobile") || ua.Contains("Android") || ua.Contains("iPhone") || ua.Contains("iPad")
                ? "Mobile" : "Desktop";
    }
}
