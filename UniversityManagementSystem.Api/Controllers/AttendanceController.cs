using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
            if (profileIdClaim == null || !int.TryParse(profileIdClaim.Value, out int profileId))
                return Unauthorized("User profile not found in token.");

            var roleClaim = User.FindFirst("role"); // or ClaimTypes.Role, depending on what AuthService sets. AuthService sets "role".
            if (roleClaim == null) return Unauthorized("User role not found.");

            try
            {
                var result = await _attendanceService.CreateSessionAsync(dto, profileId, roleClaim.Value);
                return Ok(result);
            }
            catch (System.UnauthorizedAccessException ex)
            {
                return Forbid(ex.Message);
            }
        }

        [HttpPost("check-in")]
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> CheckIn([FromBody] RecordAttendanceDto dto)
        {

            // Ideally we need StudentId here, not just UserId. 
            // But for now, let's assume UserId maps to Student or we need to lookup.
            // Simplified: The service expects StudentId. 
            // We need a way to get StudentId from UserId in the Controller or Service.
            // For this implementation, I'll pass UserId to the service and let it resolve strict mapping if needed, 
            // OR assuming the JWT contains the StudentId claim.
            // Let's assume the JWT has a specific claim "ProfileId" for StudentId/DoctorId.

            var studentIdClaim = User.FindFirst("ProfileId");
            if (studentIdClaim == null || !int.TryParse(studentIdClaim.Value, out int studentId) || studentId == 0)
                return Unauthorized("Student profile not found in token.");

            var success = await _attendanceService.RecordAttendanceAsync(studentId, dto);
            if (!success) return BadRequest("Failed to record attendance. Session might be invalid or closed.");

            return Ok(new { Message = "Attendance recorded successfully." });
        }

        [HttpGet("student/{studentId}/report")]
        [Authorize(Roles = "Doctor,TeachingAssistant,SuperAdmin,Admin")]
        public async Task<IActionResult> GetReport(int studentId, [FromQuery] int subjectId)
        {
            var report = await _attendanceService.GetStudentAttendanceAsync(studentId, subjectId);
            return Ok(report);
        }

        [HttpPost("correct")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<IActionResult> CorrectAttendance([FromQuery] int sessionId, [FromQuery] int studentId, [FromQuery] bool isPresent)
        {
            await _attendanceService.UpdateAttendanceAsync(sessionId, studentId, isPresent);
            return Ok(new { Message = "Attendance corrected successfully." });
        }

        [HttpGet("record/{sessionId}/{studentId}")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<AttendanceResponseDto>> GetRecord(int sessionId, int studentId)
        {
            var result = await _attendanceService.GetByIdAsync(sessionId, studentId);
            return Ok(result);
        }

        [HttpPut("record/{sessionId}/{studentId}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateRecord(int sessionId, int studentId, [FromBody] UpdateAttendanceDto dto)
        {
            await _attendanceService.UpdateAttendanceAsync(sessionId, studentId, dto.IsPresent);
            return NoContent();
        }

        [HttpDelete("record/{sessionId}/{studentId}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteRecord(int sessionId, int studentId)
        {
            await _attendanceService.DeleteAttendanceAsync(sessionId, studentId);
            return NoContent();
        }
    }
}
