using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using NUlid;
using UniversityManagementSystem.Core.DTOs;
using UniversityManagementSystem.Core.Entities;
using UniversityManagementSystem.Core.Interfaces;
using UniversityManagementSystem.Infrastructure.Data;

namespace UniversityManagementSystem.Infrastructure.Services
{
    public class ProctoringService(AppDbContext context) : IProctoringService
    {
        private readonly AppDbContext _context = context;

        // ── Record a single proctoring event ─────────────────────────────────
        public async Task RecordEventAsync(RecordProctoringEventDto dto, Ulid studentId)
        {
            if (!Ulid.TryParse(dto.ExamSubmissionId, out var submissionId))
                throw new ArgumentException("Invalid ExamSubmissionId.");

            // Find the submission to get ExamId
            var submission = await _context.ExamSubmissions
                .AsNoTracking()
                .Where(s => s.Id == submissionId && s.StudentId == studentId && s.DeletedAt == null)
                .Select(s => new { s.Id, s.ExamId, s.StudentId })
                .FirstOrDefaultAsync()
                ?? throw new KeyNotFoundException("Submission not found or does not belong to this student.");

            // Find or create the proctoring log
            var log = await _context.ExamProctoringLogs
                .Where(l => l.ExamSubmissionId == submissionId && l.DeletedAt == null)
                .FirstOrDefaultAsync();

            if (log == null)
            {
                log = new ExamProctoringLog
                {
                    ExamSubmissionId = submissionId,
                    StudentId        = submission.StudentId,
                    ExamId           = submission.ExamId,
                    Status           = ProctoringStatus.Clean,
                };
                _context.ExamProctoringLogs.Add(log);
            }

            // Increment counters
            switch (dto.EventType?.ToLowerInvariant())
            {
                case "tab_switch":
                    log.TabSwitchCount++;
                    break;
                case "fullscreen_exit":
                    log.FullscreenExitCount++;
                    break;
                case "copy_attempt":
                case "right_click":
                case "focus_loss":
                    log.SuspiciousActivityCount++;
                    break;
            }

            // Append event to JSON array
            var events = string.IsNullOrWhiteSpace(log.EventsJson)
                ? new List<Dictionary<string, object?>>()
                : JsonSerializer.Deserialize<List<Dictionary<string, object?>>>(log.EventsJson)
                  ?? new List<Dictionary<string, object?>>();

            events.Add(new Dictionary<string, object?>
            {
                ["type"]      = dto.EventType,
                ["timestamp"] = DateTime.UtcNow.ToString("o"),
                ["details"]   = dto.Details,
            });
            log.EventsJson = JsonSerializer.Serialize(events);

            // Auto-flag logic
            if (log.Status != ProctoringStatus.Flagged)
            {
                if (log.TabSwitchCount > 5 || log.SuspiciousActivityCount > 3)
                    log.Status = ProctoringStatus.Suspicious;
            }

            await _context.SaveChangesAsync();
        }

        // ── Full report for a submission ──────────────────────────────────────
        public async Task<ProctoringReportDto> GetReportAsync(Ulid submissionId)
        {
            var log = await _context.ExamProctoringLogs
                .AsNoTracking()
                .Where(l => l.ExamSubmissionId == submissionId && l.DeletedAt == null)
                .Select(l => new
                {
                    l.ExamSubmissionId,
                    l.TabSwitchCount,
                    l.FullscreenExitCount,
                    l.SuspiciousActivityCount,
                    l.EventsJson,
                    l.Status,
                    StudentName = l.Student.FullName,
                })
                .FirstOrDefaultAsync()
                ?? throw new KeyNotFoundException("Proctoring log not found for this submission.");

            var events = ParseEvents(log.EventsJson);

            return new ProctoringReportDto(
                submissionId.ToString(),
                log.StudentName,
                log.TabSwitchCount,
                log.FullscreenExitCount,
                log.SuspiciousActivityCount,
                log.Status.ToString(),
                events);
        }

        // ── Summary for all students in an exam ───────────────────────────────
        public async Task<List<ProctoringStudentSummaryDto>> GetExamSummaryAsync(Ulid examId)
        {
            var logs = await _context.ExamProctoringLogs
                .AsNoTracking()
                .Where(l => l.ExamId == examId && l.DeletedAt == null)
                .Select(l => new ProctoringStudentSummaryDto
                {
                    SubmissionId    = l.ExamSubmissionId.ToString(),
                    StudentName     = l.Student.FullName,
                    StudentCode     = l.Student.Code,
                    TabSwitches     = l.TabSwitchCount,
                    FullscreenExits = l.FullscreenExitCount,
                    SuspiciousCount = l.SuspiciousActivityCount,
                    Status          = l.Status.ToString(),
                    DoctorNote      = l.DoctorNote,
                })
                .OrderByDescending(l => l.TabSwitches + l.SuspiciousCount)
                .ToListAsync();

            return logs;
        }

        // ── Flag a submission manually ────────────────────────────────────────
        public async Task FlagSubmissionAsync(FlagSubmissionDto dto)
        {
            if (!Ulid.TryParse(dto.SubmissionId, out var submissionId))
                throw new ArgumentException("Invalid SubmissionId.");

            var log = await _context.ExamProctoringLogs
                .Where(l => l.ExamSubmissionId == submissionId && l.DeletedAt == null)
                .FirstOrDefaultAsync()
                ?? throw new KeyNotFoundException("Proctoring log not found.");

            log.Status     = ProctoringStatus.Flagged;
            log.DoctorNote = dto.Note;
            await _context.SaveChangesAsync();
        }

        // ── Private helpers ───────────────────────────────────────────────────
        private static List<ProctoringEventDto> ParseEvents(string eventsJson)
        {
            if (string.IsNullOrWhiteSpace(eventsJson))
                return new List<ProctoringEventDto>();

            try
            {
                var raw = JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(eventsJson)
                          ?? new();

                return raw.Select(e => new ProctoringEventDto(
                    e.TryGetValue("type",      out var t) ? t.GetString() ?? "" : "",
                    e.TryGetValue("timestamp", out var ts) && DateTime.TryParse(ts.GetString(), out var dt)
                        ? dt
                        : DateTime.UtcNow,
                    e.TryGetValue("details",   out var d) ? d.GetString() : null
                )).ToList();
            }
            catch
            {
                return new List<ProctoringEventDto>();
            }
        }
    }
}
