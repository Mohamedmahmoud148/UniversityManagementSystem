using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniversityManagementSystem.Core.Interfaces;

namespace UniversityManagementSystem.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class GradesController(IGradeService gradeService) : ControllerBase
    {
        [HttpPost("calculate/{offeringId}")]
        [Authorize(Roles = "Doctor")]
        public async Task<IActionResult> CalculateGrades(int offeringId)
        {
            var profileClaim = User.Claims.FirstOrDefault(c => c.Type == "ProfileId");
            if (profileClaim == null) return Unauthorized("ProfileId claim not found.");
            var doctorId = int.Parse(profileClaim.Value);

            var count = await gradeService.CalculateGradesForOfferingAsync(offeringId, doctorId);
            return Ok(new { message = $"Grades calculated successfully for {count} students." });
        }

        [HttpPost("{gradeId}/recalculate")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<IActionResult> RecalculateGrade(int gradeId)
        {
            await gradeService.RecalculateStudentGradeAsync(gradeId);
            return Ok(new { message = "Grade recalculated successfully." });
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<UniversityManagementSystem.Core.DTOs.GradeDto>> UpdateGrade(int id, UniversityManagementSystem.Core.DTOs.UpdateGradeDto dto)
        {
            var result = await gradeService.UpdateGradeAsync(id, dto);
            return Ok(result);
        }

        [HttpDelete("{gradeId}")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<IActionResult> InvalidateGrade(int gradeId)
        {
            await gradeService.InvalidateGradeAsync(gradeId);
            return NoContent();
        }
    }
}
