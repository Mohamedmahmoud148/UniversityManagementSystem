using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NUlid;
using UniversityManagementSystem.Core.DTOs;
using UniversityManagementSystem.Core.Interfaces;

namespace UniversityManagementSystem.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class GpaController(IGradeService gradeService) : ControllerBase
    {
        [HttpGet("my-gpa")]
        [Authorize(Roles = "Student")]
        public async Task<ActionResult<StudentGpaDto>> GetMyGpa()
        {
            var profileClaim = User.Claims.FirstOrDefault(c => c.Type == "ProfileId");
            if (profileClaim == null) return Unauthorized("ProfileId claim not found.");
            if (!Ulid.TryParse(profileClaim.Value, out var studentId)) return Unauthorized("Invalid ProfileId claim.");

            var gpaDto = await gradeService.CalculateStudentGpaAsync(studentId);
            return Ok(gpaDto);
        }

        [HttpGet("student/{studentId}")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<StudentGpaDto>> GetStudentGpa(string studentId)
        {
            if (!Ulid.TryParse(studentId, out var uid)) return BadRequest("Invalid Student ID.");
            var gpaDto = await gradeService.CalculateStudentGpaAsync(uid);
            return Ok(gpaDto);
        }

        [HttpPost("student/{studentId}/recalculate")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<ActionResult<StudentGpaDto>> RecalculateGpa(string studentId)
        {
            if (!Ulid.TryParse(studentId, out var uid)) return BadRequest("Invalid Student ID.");
            var gpaDto = await gradeService.CalculateStudentGpaAsync(uid);
            return Ok(gpaDto);
        }
    }
}
