using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniversityManagementSystem.Core.Interfaces;
using NUlid;

namespace UniversityManagementSystem.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class GradesController(IGradeService gradeService) : ControllerBase
    {
        [HttpPost("calculate/{offeringId}")]
        [Authorize(Roles = "Doctor")]
        public async Task<IActionResult> CalculateGrades(string offeringId)
        {
            if (!Ulid.TryParse(offeringId, out var oId)) return BadRequest("Invalid Offering ID.");
            var profileClaim = User.Claims.FirstOrDefault(c => c.Type == "ProfileId");
            if (profileClaim == null) return Unauthorized("ProfileId claim not found.");
            var doctorId = Ulid.Parse(profileClaim.Value);

            var count = await gradeService.CalculateGradesForOfferingAsync(oId, doctorId);
            return Ok(new { message = $"Grades calculated successfully for {count} students." });
        }

        [HttpPost("{gradeId}/recalculate")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<IActionResult> RecalculateGrade(string gradeId)
        {
            if (!Ulid.TryParse(gradeId, out var gId)) return BadRequest("Invalid Grade ID.");
            await gradeService.RecalculateStudentGradeAsync(gId);
            return Ok(new { message = "Grade recalculated successfully." });
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<UniversityManagementSystem.Core.DTOs.GradeDto>> UpdateGrade(string id, UniversityManagementSystem.Core.DTOs.UpdateGradeDto dto)
        {
            if (!Ulid.TryParse(id, out var gradeId)) return BadRequest("Invalid Grade ID.");
            var result = await gradeService.UpdateGradeAsync(gradeId, dto);
            return Ok(result);
        }

        [HttpDelete("{gradeId}")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<IActionResult> InvalidateGrade(string gradeId)
        {
            if (!Ulid.TryParse(gradeId, out var gId)) return BadRequest("Invalid Grade ID.");
            await gradeService.InvalidateGradeAsync(gId);
            return NoContent();
        }
    }
}
