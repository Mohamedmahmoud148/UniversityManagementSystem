using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniversityManagementSystem.Core.Interfaces;
using NUlid;

using Microsoft.AspNetCore.Http;

namespace UniversityManagementSystem.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class GradesController(IGradeService gradeService, IExcelImportService excelImportService, IFileService fileService, IUserContextService userContextService) : ControllerBase
    {
        [HttpPost("import/{offeringId}")]
        [Authorize(Roles = "Doctor,Admin,SuperAdmin")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> ImportGrades(string offeringId, IFormFile file)
        {
            if (!Ulid.TryParse(offeringId, out var oId)) return BadRequest("Invalid Offering ID.");
            
            var profileClaim = User.Claims.FirstOrDefault(c => c.Type == "ProfileId");
            if (profileClaim == null) return Unauthorized("ProfileId claim not found.");
            var doctorId = Ulid.Parse(profileClaim.Value);

            // Upload the Excel file to R2 bucket for archiving/auditing
            var userId = userContextService.GetUserId();
            var uploadedFileResult = await fileService.UploadFormFileAsync(userId, file);

            var importResult = await excelImportService.ImportGradesFromExcelAsync(oId, doctorId, file);
            importResult.UploadedFileId = uploadedFileResult.FileId.ToString();
            
            // If there were any imported grades (successful ones), trigger recalculation automatically
            if (importResult.Imported > 0)
            {
                await gradeService.CalculateGradesForOfferingAsync(oId, doctorId);
            }

            return Ok(importResult);
        }

        [HttpPost("calculate/{offeringId}")]
        [Authorize(Roles = "Doctor,SuperAdmin")]
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
        [Authorize(Roles = "Admin,SuperAdmin")]
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

        /// <summary>Student views their own grades across all subjects.</summary>
        [HttpGet("my-grades")]
        [Authorize(Roles = "Student,SuperAdmin")]
        public async Task<IActionResult> GetMyGrades()
        {
            var profileClaim = User.Claims.FirstOrDefault(c => c.Type == "ProfileId");
            if (profileClaim == null) return Unauthorized("ProfileId claim not found.");
            var studentId = Ulid.Parse(profileClaim.Value);

            var grades = await gradeService.GetStudentGradesAsync(studentId);
            return Ok(grades);
        }

        /// <summary>Student gets their GPA summary.</summary>
        [HttpGet("my-gpa")]
        [Authorize(Roles = "Student,SuperAdmin")]
        public async Task<IActionResult> GetMyGpa()
        {
            var profileClaim = User.Claims.FirstOrDefault(c => c.Type == "ProfileId");
            if (profileClaim == null) return Unauthorized("ProfileId claim not found.");
            var studentId = Ulid.Parse(profileClaim.Value);

            var gpa = await gradeService.CalculateStudentGpaAsync(studentId);
            return Ok(gpa);
        }
    }
}
