using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniversityManagementSystem.Core.DTOs;
using UniversityManagementSystem.Core.Interfaces;
using NUlid;

namespace UniversityManagementSystem.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SubmissionsController(IExamService examService) : ControllerBase
    {
        /// <summary>
        /// Doctor: get full answer detail for a single submission ("View Answers").
        /// </summary>
        [HttpGet("{submissionId}")]
        [Authorize(Roles = "Doctor,SuperAdmin")]
        public async Task<IActionResult> GetSubmissionDetail(string submissionId)
        {
            if (!Ulid.TryParse(submissionId, out var sid))
                return BadRequest("Invalid submission ID.");

            var profileClaim = User.Claims.FirstOrDefault(c => c.Type == "ProfileId");
            if (profileClaim == null) return Unauthorized("ProfileId claim not found.");
            var doctorId = Ulid.Parse(profileClaim.Value);

            try
            {
                var detail = await examService.GetSubmissionDetailAsync(sid, doctorId);
                return Ok(detail);
            }
            catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
            catch (UnauthorizedAccessException) { return Forbid(); }
        }

        /// <summary>
        /// Doctor: grade a single essay question in a submission and recompute total score.
        /// </summary>
        [HttpPost("{submissionId}/grade-question")]
        [Authorize(Roles = "Doctor,SuperAdmin")]
        public async Task<IActionResult> GradeQuestion(string submissionId, [FromBody] GradeQuestionDto dto)
        {
            if (!Ulid.TryParse(submissionId, out var sid))
                return BadRequest("Invalid submission ID.");

            var profileClaim = User.Claims.FirstOrDefault(c => c.Type == "ProfileId");
            if (profileClaim == null) return Unauthorized("ProfileId claim not found.");
            var doctorId = Ulid.Parse(profileClaim.Value);

            try
            {
                await examService.GradeQuestionAsync(sid, dto, doctorId);
                return Ok(new { message = "Question graded successfully." });
            }
            catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
            catch (UnauthorizedAccessException) { return Forbid(); }
            catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
        }
    }
}
