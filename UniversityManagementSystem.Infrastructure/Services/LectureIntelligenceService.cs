using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NUlid;
using UniversityManagementSystem.Api.Hubs;
using UniversityManagementSystem.Core.DTOs.Lecture;
using UniversityManagementSystem.Core.Entities;
using UniversityManagementSystem.Core.Interfaces;
using UniversityManagementSystem.Infrastructure.Data;

namespace UniversityManagementSystem.Infrastructure.Services
{
    public class LectureIntelligenceService(
        AppDbContext context,
        IStorageService storage,
        ISpeechToTextService stt,
        HttpClient httpClient,
        IHubContext<NotificationHub> hub,
        ILogger<LectureIntelligenceService> logger) : ILectureIntelligenceService
    {
        private static readonly JsonSerializerOptions _json = new()
        {
            PropertyNamingPolicy        = JsonNamingPolicy.SnakeCaseLower,
            PropertyNameCaseInsensitive = true
        };

        // ── Upload ────────────────────────────────────────────────────────────

        public async Task<LectureRecordingDto> UploadAsync(
            Ulid studentId, byte[] audioBytes, string fileName,
            string mimeType, long fileSize)
        {
            // Save to R2 storage
            using var ms = new System.IO.MemoryStream(audioBytes);
            var storagePath = await storage.UploadAsync(ms, fileName, mimeType, "lecture-recordings");

            var recording = new LectureRecording
            {
                StudentId        = studentId,
                FileName         = storagePath,
                OriginalFileName = fileName,
                StoragePath      = storagePath,
                MimeType         = mimeType,
                FileSize         = fileSize,
                Status           = LectureRecordingStatus.Uploading,
                CreatedAt        = DateTime.UtcNow
            };

            context.LectureRecordings.Add(recording);
            await context.SaveChangesAsync();

            logger.LogInformation("LectureIntelligence: created recording {Id} for student {StudentId}", recording.Id, studentId);
            return ToDto(recording);
        }

        // ── Get ───────────────────────────────────────────────────────────────

        public async Task<LectureRecordingDto?> GetAsync(Ulid recordingId, Ulid studentId)
        {
            var r = await context.LectureRecordings
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == recordingId && r.StudentId == studentId);
            return r == null ? null : ToDto(r);
        }

        public async Task<List<LectureRecordingDto>> GetMyRecordingsAsync(Ulid studentId)
        {
            var list = await context.LectureRecordings
                .AsNoTracking()
                .Where(r => r.StudentId == studentId)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();
            return list.Select(ToDto).ToList();
        }

        // ── Summary ───────────────────────────────────────────────────────────

        public async Task<LectureSummaryDto?> GetSummaryAsync(Ulid recordingId, Ulid studentId)
        {
            var recording = await context.LectureRecordings.AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == recordingId && r.StudentId == studentId);
            if (recording == null) return null;

            var summary = await context.LectureSummaries.AsNoTracking()
                .FirstOrDefaultAsync(s => s.RecordingId == recordingId);
            if (summary == null) return null;

            return new LectureSummaryDto
            {
                RecordingId        = recordingId.ToString(),
                Summary            = summary.Summary,
                KeyConcepts        = Deserialize<List<string>>(summary.KeyConceptsJson) ?? new(),
                Timeline           = Deserialize<List<LectureTimelineSection>>(summary.TimelineJson) ?? new(),
                SuggestedQuestions = Deserialize<List<LectureSuggestedQuestion>>(summary.SuggestedQuestionsJson) ?? new()
            };
        }

        // ── Flashcards ────────────────────────────────────────────────────────

        public async Task<List<LectureFlashcardDto>> GetFlashcardsAsync(Ulid recordingId, Ulid studentId)
        {
            var ok = await OwnsRecordingAsync(recordingId, studentId);
            if (!ok) return new();

            return await context.LectureFlashcards.AsNoTracking()
                .Where(f => f.RecordingId == recordingId)
                .Select(f => new LectureFlashcardDto
                {
                    Id    = f.Id.ToString(),
                    Front = f.Front,
                    Back  = f.Back
                }).ToListAsync();
        }

        // ── Quiz ──────────────────────────────────────────────────────────────

        public async Task<List<LectureQuizDto>> GetQuizAsync(Ulid recordingId, Ulid studentId)
        {
            var ok = await OwnsRecordingAsync(recordingId, studentId);
            if (!ok) return new();

            return await context.LectureQuizzes.AsNoTracking()
                .Where(q => q.RecordingId == recordingId)
                .Select(q => new LectureQuizDto
                {
                    Id            = q.Id.ToString(),
                    Question      = q.Question,
                    OptionA       = q.OptionA,
                    OptionB       = q.OptionB,
                    OptionC       = q.OptionC,
                    OptionD       = q.OptionD,
                    CorrectAnswer = q.CorrectAnswer,
                    Explanation   = q.Explanation
                }).ToListAsync();
        }

        // ── Ask AI ────────────────────────────────────────────────────────────

        public async Task<string> AskAsync(Ulid recordingId, Ulid studentId, string message)
        {
            var ok = await OwnsRecordingAsync(recordingId, studentId);
            if (!ok) return "لا تملك صلاحية الوصول لهذا التسجيل.";

            // Build transcript context from chunks
            var chunks = await context.LectureTranscripts.AsNoTracking()
                .Where(t => t.RecordingId == recordingId)
                .OrderBy(t => t.ChunkIndex)
                .Select(t => t.Text)
                .ToListAsync();

            var transcriptContext = string.Join("\n\n", chunks).Trim();
            if (string.IsNullOrEmpty(transcriptContext))
                return "لم يتم إنشاء transcript لهذا التسجيل بعد.";

            try
            {
                var payload = new
                {
                    transcript = transcriptContext[..Math.Min(transcriptContext.Length, 8_000)],
                    message
                };
                using var resp = await httpClient.PostAsJsonAsync("/api/lecture/ask", payload, _json);
                resp.EnsureSuccessStatusCode();
                var result = await resp.Content.ReadFromJsonAsync<FastApiAskResponse>(_json);
                return result?.Answer ?? "لم أتمكن من الإجابة. حاول مرة أخرى.";
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "LectureIntelligence: Ask failed for recording {Id}", recordingId);
                return "حدث خطأ أثناء معالجة سؤالك. حاول مرة أخرى.";
            }
        }

        // ── Dashboard ─────────────────────────────────────────────────────────

        public async Task<LectureDashboardDto> GetDashboardAsync(Ulid studentId)
        {
            var recordings = await context.LectureRecordings.AsNoTracking()
                .Where(r => r.StudentId == studentId)
                .ToListAsync();

            var totalSeconds = recordings.Where(r => r.DurationSeconds.HasValue)
                .Sum(r => r.DurationSeconds!.Value);

            var flashcardCount = await context.LectureFlashcards.AsNoTracking()
                .CountAsync(f => context.LectureRecordings
                    .Where(r => r.StudentId == studentId)
                    .Select(r => r.Id).Contains(f.RecordingId));

            var quizCount = await context.LectureQuizzes.AsNoTracking()
                .CountAsync(q => context.LectureRecordings
                    .Where(r => r.StudentId == studentId)
                    .Select(r => r.Id).Contains(q.RecordingId));

            return new LectureDashboardDto
            {
                TotalRecordings    = recordings.Count,
                TotalStudyHours    = totalSeconds / 3600,
                TotalFlashcards    = flashcardCount,
                TotalQuizQuestions = quizCount,
                RecentRecordings   = recordings
                    .OrderByDescending(r => r.CreatedAt)
                    .Take(5)
                    .Select(ToDto).ToList()
            };
        }

        // ── Delete ────────────────────────────────────────────────────────────

        public async Task DeleteAsync(Ulid recordingId, Ulid studentId)
        {
            var recording = await context.LectureRecordings
                .FirstOrDefaultAsync(r => r.Id == recordingId && r.StudentId == studentId)
                ?? throw new KeyNotFoundException("Recording not found.");

            // Delete from storage
            if (!string.IsNullOrEmpty(recording.StoragePath))
            {
                try { await storage.DeleteAsync(recording.StoragePath); }
                catch (Exception ex) { logger.LogWarning(ex, "Could not delete storage file for recording {Id}", recordingId); }
            }

            recording.DeletedAt = DateTime.UtcNow;
            await context.SaveChangesAsync();
        }

        // ── Processing Pipeline (called by Hangfire) ──────────────────────────

        public async Task ProcessRecordingAsync(Ulid recordingId)
        {
            var recording = await context.LectureRecordings
                .FirstOrDefaultAsync(r => r.Id == recordingId);
            if (recording == null) return;

            var studentUserId = await context.Students.AsNoTracking()
                .Where(s => s.Id == recording.StudentId)
                .Select(s => (Ulid?)s.SystemUserId)
                .FirstOrDefaultAsync();

            async Task PushStatus(string eventName, object payload)
            {
                if (studentUserId.HasValue)
                    await hub.Clients.Group(studentUserId.Value.ToString())
                        .SendAsync(eventName, payload);
            }

            try
            {
                // ── Step 1: Transcribe ─────────────────────────────────────────
                recording.Status = LectureRecordingStatus.Transcribing;
                await context.SaveChangesAsync();
                await PushStatus("LectureStatusChanged", new { recordingId = recordingId.ToString(), status = "Transcribing" });

                var audioStream = await storage.DownloadAsync(recording.StoragePath);
                using var ms = new System.IO.MemoryStream();
                await audioStream.CopyToAsync(ms);
                var audioBytes = ms.ToArray();

                var sttResult = await stt.TranscribeAsync(audioBytes, recording.OriginalFileName, recording.MimeType);
                if (sttResult == null || string.IsNullOrWhiteSpace(sttResult.Transcript))
                    throw new Exception("Speech-to-text returned empty transcript.");

                // ── Step 2: Save transcript chunks (every ~500 words) ──────────
                var transcript = sttResult.Transcript;
                recording.TranscriptChars  = transcript.Length;
                recording.DurationSeconds  = sttResult.DurationSeconds;

                var words  = transcript.Split(' ');
                int chunkSize = 500;
                var chunks = new List<string>();
                for (int i = 0; i < words.Length; i += chunkSize)
                    chunks.Add(string.Join(" ", words.Skip(i).Take(chunkSize)));

                context.LectureTranscripts.RemoveRange(
                    await context.LectureTranscripts.Where(t => t.RecordingId == recordingId).ToListAsync());

                for (int i = 0; i < chunks.Count; i++)
                {
                    context.LectureTranscripts.Add(new LectureTranscript
                    {
                        RecordingId = recordingId,
                        ChunkIndex  = i,
                        Text        = chunks[i],
                        CreatedAt   = DateTime.UtcNow
                    });
                }
                await context.SaveChangesAsync();
                await PushStatus("TranscriptionCompleted", new { recordingId = recordingId.ToString() });

                // ── Step 3: AI Analysis ────────────────────────────────────────
                recording.Status = LectureRecordingStatus.Analyzing;
                await context.SaveChangesAsync();
                await PushStatus("LectureStatusChanged", new { recordingId = recordingId.ToString(), status = "Analyzing" });

                var truncated = transcript[..Math.Min(transcript.Length, 10_000)];
                var analyzePayload = new { transcript = truncated };

                using var analyzeResp = await httpClient.PostAsJsonAsync("/api/lecture/analyze", analyzePayload, _json);
                analyzeResp.EnsureSuccessStatusCode();
                var analysis = await analyzeResp.Content.ReadFromJsonAsync<FastApiAnalyzeResponse>(_json);

                if (analysis != null)
                {
                    // Save summary
                    var existingSummary = await context.LectureSummaries.FirstOrDefaultAsync(s => s.RecordingId == recordingId);
                    if (existingSummary != null) context.LectureSummaries.Remove(existingSummary);

                    context.LectureSummaries.Add(new LectureSummary
                    {
                        RecordingId            = recordingId,
                        Summary                = analysis.Summary,
                        KeyConceptsJson        = JsonSerializer.Serialize(analysis.KeyConcepts),
                        TimelineJson           = JsonSerializer.Serialize(analysis.Timeline, _json),
                        SuggestedQuestionsJson = JsonSerializer.Serialize(analysis.SuggestedQuestions, _json),
                        CreatedAt              = DateTime.UtcNow
                    });

                    // Save flashcards
                    context.LectureFlashcards.RemoveRange(
                        await context.LectureFlashcards.Where(f => f.RecordingId == recordingId).ToListAsync());
                    foreach (var fc in analysis.Flashcards)
                        context.LectureFlashcards.Add(new LectureFlashcard
                        {
                            RecordingId = recordingId,
                            Front       = fc.Front,
                            Back        = fc.Back,
                            CreatedAt   = DateTime.UtcNow
                        });

                    // Save quiz
                    context.LectureQuizzes.RemoveRange(
                        await context.LectureQuizzes.Where(q => q.RecordingId == recordingId).ToListAsync());
                    foreach (var q in analysis.Quiz)
                        context.LectureQuizzes.Add(new LectureQuiz
                        {
                            RecordingId   = recordingId,
                            Question      = q.Question,
                            OptionA       = q.OptionA,
                            OptionB       = q.OptionB,
                            OptionC       = q.OptionC,
                            OptionD       = q.OptionD,
                            CorrectAnswer = q.CorrectAnswer,
                            Explanation   = q.Explanation,
                            CreatedAt     = DateTime.UtcNow
                        });

                    await context.SaveChangesAsync();
                    await PushStatus("AnalysisCompleted", new { recordingId = recordingId.ToString() });
                    await PushStatus("FlashcardsGenerated", new { recordingId = recordingId.ToString(), count = analysis.Flashcards.Count });
                    await PushStatus("QuizGenerated", new { recordingId = recordingId.ToString(), count = analysis.Quiz.Count });
                }

                // ── Step 4: Mark completed ─────────────────────────────────────
                recording.Status      = LectureRecordingStatus.Completed;
                recording.ProcessedAt = DateTime.UtcNow;
                await context.SaveChangesAsync();
                await PushStatus("LectureStatusChanged", new { recordingId = recordingId.ToString(), status = "Completed" });

                logger.LogInformation("LectureIntelligence: processing completed for {Id}", recordingId);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "LectureIntelligence: processing failed for {Id}", recordingId);
                recording.Status       = LectureRecordingStatus.Failed;
                recording.ErrorMessage = ex.Message;
                await context.SaveChangesAsync();
                await PushStatus("LectureStatusChanged", new { recordingId = recordingId.ToString(), status = "Failed", error = ex.Message });
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private async Task<bool> OwnsRecordingAsync(Ulid recordingId, Ulid studentId) =>
            await context.LectureRecordings.AsNoTracking()
                .AnyAsync(r => r.Id == recordingId && r.StudentId == studentId);

        private static LectureRecordingDto ToDto(LectureRecording r) => new()
        {
            Id               = r.Id.ToString(),
            FileName         = r.FileName,
            OriginalFileName = r.OriginalFileName,
            FileSize         = r.FileSize,
            DurationSeconds  = r.DurationSeconds,
            Status           = r.Status.ToString(),
            TranscriptChars  = r.TranscriptChars,
            ErrorMessage     = r.ErrorMessage,
            CreatedAt        = r.CreatedAt,
            ProcessedAt      = r.ProcessedAt
        };

        private static T? Deserialize<T>(string json) where T : class
        {
            try { return JsonSerializer.Deserialize<T>(json); }
            catch { return null; }
        }
    }
}
