using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NUlid;
using UniversityManagementSystem.Core.DTOs;
using UniversityManagementSystem.Core.Interfaces;

namespace UniversityManagementSystem.Api.Controllers
{
    /// <summary>
    /// Phase 6: Exam Proctoring — record events, view reports, flag suspicious submissions.
    /// </summary>
    [ApiController]
    [Route("api/proctoring")]
    public class ProctoringController(IProctoringService proctoringService) : ControllerBase
    {
        private readonly IProctoringService _proctoringService = proctoringService;

        // ── POST /api/proctoring/event ────────────────────────────────────────
        /// <summary>
        /// Student records a single proctoring event during an exam (tab switch, etc.).
        /// </summary>
        [HttpPost("event")]
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> RecordEvent([FromBody] RecordProctoringEventDto dto)
        {
            var profileId = User.Claims.FirstOrDefault(c => c.Type == "ProfileId")?.Value;
            if (profileId == null || !Ulid.TryParse(profileId, out var studentId))
                return Unauthorized("ProfileId claim missing.");

            await _proctoringService.RecordEventAsync(dto, studentId);
            return Ok(new { message = "Event recorded." });
        }

        // ── GET /api/proctoring/report/{submissionId} ─────────────────────────
        /// <summary>
        /// Returns the full proctoring report for a submission.
        /// </summary>
        [HttpGet("report/{submissionId}")]
        [Authorize(Roles = "Doctor,Admin,SuperAdmin")]
        public async Task<IActionResult> GetReport(string submissionId)
        {
            if (!Ulid.TryParse(submissionId, out var sId))
                return BadRequest("Invalid submissionId.");

            var report = await _proctoringService.GetReportAsync(sId);
            return Ok(report);
        }

        // ── GET /api/proctoring/exam/{examId}/summary ─────────────────────────
        /// <summary>
        /// Returns a summary of all students' proctoring data for an exam.
        /// </summary>
        [HttpGet("exam/{examId}/summary")]
        [Authorize(Roles = "Doctor,Admin,SuperAdmin")]
        public async Task<IActionResult> GetExamSummary(string examId)
        {
            if (!Ulid.TryParse(examId, out var eId))
                return BadRequest("Invalid examId.");

            var summary = await _proctoringService.GetExamSummaryAsync(eId);
            return Ok(summary);
        }

        // ── POST /api/proctoring/flag/{submissionId} ──────────────────────────
        /// <summary>
        /// Doctor manually flags a submission as suspicious.
        /// </summary>
        [HttpPost("flag/{submissionId}")]
        [Authorize(Roles = "Doctor,Admin,SuperAdmin")]
        public async Task<IActionResult> FlagSubmission(string submissionId, [FromBody] FlagSubmissionDto dto)
        {
            // Ensure the SubmissionId in route matches body (use route value as source of truth)
            var flagDto = dto with { SubmissionId = submissionId };
            await _proctoringService.FlagSubmissionAsync(flagDto);
            return Ok(new { message = "Submission flagged." });
        }
    }
}
