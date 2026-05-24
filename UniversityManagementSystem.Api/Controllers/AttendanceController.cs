using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NUlid;
using System.Security.Claims;
using UniversityManagementSystem.Core.DTOs;
using UniversityManagementSystem.Core.Interfaces;

namespace UniversityManagementSystem.Api.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class AttendanceController(IAttendanceService attendanceService) : ControllerBase
    {
        private readonly IAttendanceService _attendanceService = attendanceService;

        [HttpPost("sessions")]
        [Authorize(Roles = "Doctor,TeachingAssistant,SuperAdmin,Admin")]
        public async Task<IActionResult> CreateSession([FromBody] CreateAttendanceSessionDto dto)
        {
            var profileIdClaim = User.FindFirst("ProfileId");
            if (profileIdClaim == null || !Ulid.TryParse(profileIdClaim.Value, out var profileId))
                return Unauthorized("User profile not found in token.");

            var roleClaim = User.FindFirst("role");
            if (roleClaim == null) return Unauthorized("User role not found.");

            try
            {
                var result = await _attendanceService.CreateSessionAsync(dto, profileId, roleClaim.Value);
                return Ok(result);
            }
            catch (System.UnauthorizedAccessException ex)
            {
                return StatusCode(403, new { message = ex.Message });
            }
        }

        [HttpPost("check-in")]
        [Authorize(Roles = "Student,SuperAdmin")]
        public async Task<IActionResult> CheckIn([FromBody] RecordAttendanceDto dto)
        {
            var studentIdClaim = User.FindFirst("ProfileId");
            if (studentIdClaim == null || !Ulid.TryParse(studentIdClaim.Value, out var studentId))
                return Unauthorized("Student profile not found in token.");

            var success = await _attendanceService.RecordAttendanceAsync(studentId, dto);
            if (!success) return BadRequest("Failed to record attendance. Session might be invalid or closed.");

            return Ok(new { Message = "Attendance recorded successfully." });
        }

        [HttpGet("student/{studentId}/report")]
        [Authorize(Roles = "Doctor,TeachingAssistant,SuperAdmin,Admin,Student")]
        public async Task<IActionResult> GetReport(string studentId, [FromQuery] string subjectId)
        {
            if (!Ulid.TryParse(studentId, out var sId)) return BadRequest("Invalid Student ID.");
            if (!Ulid.TryParse(subjectId, out var subId)) return BadRequest("Invalid Subject ID.");

            var roleClaim = User.FindFirst("role")?.Value;
            if (roleClaim == "Student")
            {
                var profileIdClaim = User.FindFirst("ProfileId");
                if (profileIdClaim == null || !Ulid.TryParse(profileIdClaim.Value, out var profileId) || profileId != sId)
                    return StatusCode(403, new { message = "Students can only view their own attendance report." });
            }

            var report = await _attendanceService.GetStudentAttendanceAsync(sId, subId);
            return Ok(report);
        }

        [HttpPost("correct")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<IActionResult> CorrectAttendance([FromQuery] string sessionId, [FromQuery] string studentId, [FromQuery] bool isPresent)
        {
            if (!Ulid.TryParse(sessionId, out var sessId)) return BadRequest("Invalid Session ID.");
            if (!Ulid.TryParse(studentId, out var studId)) return BadRequest("Invalid Student ID.");
            await _attendanceService.UpdateAttendanceAsync(sessId, studId, isPresent);
            return Ok(new { Message = "Attendance corrected successfully." });
        }

        [HttpGet("record/{sessionId}/{studentId}")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<ActionResult<AttendanceResponseDto>> GetRecord(string sessionId, string studentId)
        {
            if (!Ulid.TryParse(sessionId, out var sessId)) return BadRequest("Invalid Session ID.");
            if (!Ulid.TryParse(studentId, out var studId)) return BadRequest("Invalid Student ID.");
            var result = await _attendanceService.GetByIdAsync(sessId, studId);
            return Ok(result);
        }

        [HttpPut("record/{sessionId}/{studentId}")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<IActionResult> UpdateRecord(string sessionId, string studentId, [FromBody] UpdateAttendanceDto dto)
        {
            if (!Ulid.TryParse(sessionId, out var sessId)) return BadRequest("Invalid Session ID.");
            if (!Ulid.TryParse(studentId, out var studId)) return BadRequest("Invalid Student ID.");
            await _attendanceService.UpdateAttendanceAsync(sessId, studId, dto.IsPresent);
            return NoContent();
        }

        [HttpDelete("record/{sessionId}/{studentId}")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<IActionResult> DeleteRecord(string sessionId, string studentId)
        {
            if (!Ulid.TryParse(sessionId, out var sessId)) return BadRequest("Invalid Session ID.");
            if (!Ulid.TryParse(studentId, out var studId)) return BadRequest("Invalid Student ID.");
            await _attendanceService.DeleteAttendanceAsync(sessId, studId);
            return NoContent();
        }
    }
}
