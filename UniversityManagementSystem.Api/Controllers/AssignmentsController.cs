using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NUlid;
using UniversityManagementSystem.Core.DTOs;
using UniversityManagementSystem.Core.Interfaces;

namespace UniversityManagementSystem.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AssignmentsController(
        IAssignmentService assignmentService,
        IStorageService storageService,
        IUserContextService userContext) : ControllerBase
    {
        private const long MaxFileSizeBytes = 100L * 1024 * 1024; // 100 MB

        // ── POST /api/assignments ─────────────────────────────────────────────
        [HttpPost]
        [Authorize(Roles = "Doctor,Admin,SuperAdmin")]
        public async Task<IActionResult> CreateAssignment([FromBody] CreateAssignmentDto dto)
        {
            var doctorId = userContext.GetProfileId();
            var result = await assignmentService.CreateAssignmentAsync(dto, doctorId);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }

        // ── GET /api/assignments/offering/{offeringId} ────────────────────────
        [HttpGet("offering/{offeringId}")]
        [Authorize]
        public async Task<IActionResult> GetByOffering(string offeringId)
        {
            if (!Ulid.TryParse(offeringId, out var oId))
                return BadRequest("Invalid offering ID.");

            var result = await assignmentService.GetOfferingAssignmentsAsync(oId);
            return Ok(result);
        }

        // ── GET /api/assignments/{id} ─────────────────────────────────────────
        [HttpGet("{id}")]
        [Authorize]
        public async Task<IActionResult> GetById(string id)
        {
            if (!Ulid.TryParse(id, out var assignmentId))
                return BadRequest("Invalid assignment ID.");

            var result = await assignmentService.GetByIdAsync(assignmentId);
            return Ok(result);
        }

        // ── POST /api/assignments/{id}/submit ─────────────────────────────────
        [HttpPost("{id}/submit")]
        [Authorize(Roles = "Student")]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(104_857_600)] // 100 MB
        public async Task<IActionResult> Submit(string id, [FromForm] string? textAnswer, IFormFile? file)
        {
            if (!Ulid.TryParse(id, out var assignmentId))
                return BadRequest("Invalid assignment ID.");

            var studentId = userContext.GetProfileId();

            string? fileUrl = null;
            string? storageKey = null;

            if (file != null && file.Length > 0)
            {
                if (file.Length > MaxFileSizeBytes)
                    return BadRequest("File exceeds the 100 MB size limit.");

                using var stream = file.OpenReadStream();
                storageKey = await storageService.UploadAsync(
                    stream,
                    file.FileName,
                    file.ContentType,
                    "assignments");

                fileUrl = storageService.BuildUrl(storageKey);
            }

            if (string.IsNullOrWhiteSpace(textAnswer) && fileUrl == null)
                return BadRequest("Either a text answer or a file must be provided.");

            var submission = await assignmentService.SubmitAsync(assignmentId, studentId, textAnswer, fileUrl, storageKey);

            return Ok(new { submissionId = submission.Id.ToString(), message = "Assignment submitted successfully." });
        }

        // ── GET /api/assignments/{id}/submissions ─────────────────────────────
        [HttpGet("{id}/submissions")]
        [Authorize(Roles = "Doctor,Admin,SuperAdmin")]
        public async Task<IActionResult> GetSubmissions(string id)
        {
            if (!Ulid.TryParse(id, out var assignmentId))
                return BadRequest("Invalid assignment ID.");

            var doctorId = userContext.GetProfileId();
            var result = await assignmentService.GetSubmissionsAsync(assignmentId, doctorId);
            return Ok(result);
        }

        // ── POST /api/assignments/submissions/{id}/grade ──────────────────────
        [HttpPost("submissions/{id}/grade")]
        [Authorize(Roles = "Doctor,Admin,SuperAdmin")]
        public async Task<IActionResult> GradeManually(string id, [FromBody] GradeAssignmentSubmissionDto dto)
        {
            if (!Ulid.TryParse(id, out var submissionId))
                return BadRequest("Invalid submission ID.");

            var doctorId = userContext.GetProfileId();
            var result = await assignmentService.GradeManuallyAsync(submissionId, dto.Grade, dto.Feedback, doctorId);
            return Ok(result);
        }

        // ── POST /api/assignments/submissions/{id}/ai-grade ───────────────────
        [HttpPost("submissions/{id}/ai-grade")]
        [Authorize(Roles = "Doctor,Admin,SuperAdmin")]
        public async Task<IActionResult> AiGrade(string id)
        {
            if (!Ulid.TryParse(id, out var submissionId))
                return BadRequest("Invalid submission ID.");

            var result = await assignmentService.TriggerAiGradingAsync(submissionId);
            return Ok(result);
        }

        // ── GET /api/assignments/{id}/my-submission ───────────────────────────
        [HttpGet("{id}/my-submission")]
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> GetMySubmission(string id)
        {
            if (!Ulid.TryParse(id, out var assignmentId))
                return BadRequest("Invalid assignment ID.");

            var studentId = userContext.GetProfileId();
            var result = await assignmentService.GetStudentSubmissionAsync(assignmentId, studentId);

            if (result == null)
                return NotFound("No submission found for this assignment.");

            return Ok(result);
        }

        // ── DELETE /api/assignments/{id} ──────────────────────────────────────
        [HttpDelete("{id}")]
        [Authorize(Roles = "Doctor,Admin,SuperAdmin")]
        public async Task<IActionResult> DeleteAssignment(string id)
        {
            if (!Ulid.TryParse(id, out var assignmentId))
                return BadRequest("Invalid assignment ID.");

            var doctorId = userContext.GetProfileId();
            await assignmentService.DeleteAssignmentAsync(assignmentId, doctorId);
            return NoContent();
        }
    }
}
