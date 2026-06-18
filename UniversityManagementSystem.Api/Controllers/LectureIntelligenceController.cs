using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Hangfire;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NUlid;
using UniversityManagementSystem.Core.DTOs.Lecture;
using UniversityManagementSystem.Core.Interfaces;
using UniversityManagementSystem.Infrastructure.Jobs;

namespace UniversityManagementSystem.Api.Controllers
{
    /// <summary>
    /// Lecture Recording Intelligence — AI-powered audio learning pipeline.
    ///
    /// Upload flow:
    ///   POST /upload → save to R2 → enqueue Hangfire job → return {recordingId, status: "Processing"}
    ///   Hangfire: Transcribe (Whisper) → AI Analyze → Save summary/flashcards/quiz → Push SignalR
    ///
    /// Students can only access their own recordings.
    /// </summary>
    [ApiController]
    [Route("api/companion/recordings")]
    [Authorize(Roles = "Student,SuperAdmin")]
    public class LectureIntelligenceController(
        ILectureIntelligenceService service,
        IUserContextService userContext,
        IBackgroundJobClient backgroundJobs) : ControllerBase
    {
        private static readonly HashSet<string> _allowedMimes = new(StringComparer.OrdinalIgnoreCase)
        {
            "audio/mpeg",           // mp3
            "audio/wav",            // wav
            "audio/x-wav",
            "audio/wave",
            "audio/mp4",            // m4a
            "audio/x-m4a",
            "audio/aac",            // aac
            "audio/x-aac",
            "audio/ogg",            // ogg
            "audio/vorbis",
            "application/ogg",
        };

        private static readonly HashSet<string> _allowedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".mp3", ".wav", ".m4a", ".aac", ".ogg"
        };

        private const long MaxBytes = 200L * 1024 * 1024; // 200 MB

        // ── POST /upload ──────────────────────────────────────────────────────

        [HttpPost("upload")]
        [RequestSizeLimit(209_715_200)] // 200 MB
        public async Task<IActionResult> Upload(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("No file provided.");

            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!_allowedExtensions.Contains(ext))
                return BadRequest($"Format '{ext}' is not supported. Supported: mp3, wav, m4a, aac, ogg.");

            if (!_allowedMimes.Contains(file.ContentType))
                return BadRequest($"MIME type '{file.ContentType}' is not accepted.");

            if (file.Length > MaxBytes)
                return BadRequest("File exceeds the 200 MB size limit.");

            var studentId = userContext.GetProfileId();

            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);
            var bytes = ms.ToArray();

            var recording = await service.UploadAsync(
                studentId, bytes, file.FileName, file.ContentType, file.Length);

            // Enqueue background processing immediately
            backgroundJobs.Enqueue<LectureProcessingJob>(
                job => job.ProcessAsync(recording.Id));

            return Ok(new
            {
                recordingId = recording.Id,
                status      = "Processing",
                message     = "تم رفع التسجيل بنجاح وجاري تحليله. ستصلك إشعار عند الانتهاء."
            });
        }

        // ── GET /{id} ─────────────────────────────────────────────────────────

        [HttpGet("{recordingId}")]
        public async Task<IActionResult> Get(string recordingId)
        {
            if (!Ulid.TryParse(recordingId, out var rid)) return BadRequest("Invalid ID.");
            var studentId = userContext.GetProfileId();
            var rec = await service.GetAsync(rid, studentId);
            return rec == null ? NotFound("Recording not found.") : Ok(rec);
        }

        // ── GET /my ───────────────────────────────────────────────────────────

        [HttpGet("my")]
        public async Task<IActionResult> GetMy()
        {
            var studentId = userContext.GetProfileId();
            var list = await service.GetMyRecordingsAsync(studentId);
            return Ok(list);
        }

        // ── GET /dashboard ────────────────────────────────────────────────────

        [HttpGet("dashboard")]
        public async Task<IActionResult> Dashboard()
        {
            var studentId = userContext.GetProfileId();
            var dash = await service.GetDashboardAsync(studentId);
            return Ok(dash);
        }

        // ── GET /{id}/summary ─────────────────────────────────────────────────

        [HttpGet("{recordingId}/summary")]
        public async Task<IActionResult> GetSummary(string recordingId)
        {
            if (!Ulid.TryParse(recordingId, out var rid)) return BadRequest("Invalid ID.");
            var studentId = userContext.GetProfileId();
            var summary = await service.GetSummaryAsync(rid, studentId);
            return summary == null ? NotFound("Summary not ready yet.") : Ok(summary);
        }

        // ── GET /{id}/flashcards ──────────────────────────────────────────────

        [HttpGet("{recordingId}/flashcards")]
        public async Task<IActionResult> GetFlashcards(string recordingId)
        {
            if (!Ulid.TryParse(recordingId, out var rid)) return BadRequest("Invalid ID.");
            var studentId = userContext.GetProfileId();
            var cards = await service.GetFlashcardsAsync(rid, studentId);
            return Ok(cards);
        }

        // ── GET /{id}/quiz ────────────────────────────────────────────────────

        [HttpGet("{recordingId}/quiz")]
        public async Task<IActionResult> GetQuiz(string recordingId)
        {
            if (!Ulid.TryParse(recordingId, out var rid)) return BadRequest("Invalid ID.");
            var studentId = userContext.GetProfileId();
            var quiz = await service.GetQuizAsync(rid, studentId);
            return Ok(quiz);
        }

        // ── POST /{id}/ask ────────────────────────────────────────────────────

        [HttpPost("{recordingId}/ask")]
        public async Task<IActionResult> Ask(string recordingId, [FromBody] LectureAskRequestDto dto)
        {
            if (!Ulid.TryParse(recordingId, out var rid)) return BadRequest("Invalid ID.");
            if (string.IsNullOrWhiteSpace(dto.Message)) return BadRequest("Message is required.");
            var studentId = userContext.GetProfileId();
            var answer = await service.AskAsync(rid, studentId, dto.Message);
            return Ok(new { answer });
        }

        // ── DELETE /{id} ──────────────────────────────────────────────────────

        [HttpDelete("{recordingId}")]
        public async Task<IActionResult> Delete(string recordingId)
        {
            if (!Ulid.TryParse(recordingId, out var rid)) return BadRequest("Invalid ID.");
            var studentId = userContext.GetProfileId();
            try
            {
                await service.DeleteAsync(rid, studentId);
                return NoContent();
            }
            catch (KeyNotFoundException) { return NotFound("Recording not found."); }
        }
    }
}
