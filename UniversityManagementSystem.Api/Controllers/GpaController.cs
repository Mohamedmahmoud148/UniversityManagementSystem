using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
            var studentId = int.Parse(profileClaim.Value);

            var gpaDto = await gradeService.CalculateStudentGpaAsync(studentId);
            return Ok(gpaDto);
        }

        [HttpGet("student/{studentId}")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<StudentGpaDto>> GetStudentGpa(int studentId)
        {
            var gpaDto = await gradeService.CalculateStudentGpaAsync(studentId);
            return Ok(gpaDto);
        }

        [HttpPost("student/{studentId}/recalculate")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<ActionResult<StudentGpaDto>> RecalculateGpa(int studentId)
        {
            // Since GPA is calculated on-the-fly, this is effectively a fresh fetch.
            // In a cached system, this would invalidate cache.
            var gpaDto = await gradeService.CalculateStudentGpaAsync(studentId);
            return Ok(gpaDto);
        }
    }
}
