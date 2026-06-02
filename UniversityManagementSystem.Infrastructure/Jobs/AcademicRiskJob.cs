using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NUlid;
using UniversityManagementSystem.Core.DTOs;
using UniversityManagementSystem.Core.Entities;
using UniversityManagementSystem.Core.Interfaces;
using UniversityManagementSystem.Infrastructure.Data;

namespace UniversityManagementSystem.Infrastructure.Jobs
{
    /// <summary>
    /// Daily Hangfire job: analyses attendance and grade data for each active SubjectOffering,
    /// assigns a RiskLevel to each enrolled student, persists AcademicRiskScore records,
    /// and pushes in-app + real-time notifications to at-risk students.
    /// </summary>
    public class AcademicRiskJob(
        AppDbContext context,
        INotificationService notificationService,
        ILogger<AcademicRiskJob> logger) : IAcademicRiskJob
    {
        private readonly AppDbContext _context = context;
        private readonly INotificationService _notifications = notificationService;
        private readonly ILogger<AcademicRiskJob> _logger = logger;

        // ── Public interface ─────────────────────────────────────────────────

        [AutomaticRetry(Attempts = 2)]
        [DisableConcurrentExecution(timeoutInSeconds: 3600)]
        public async Task RunDailyRiskAnalysisAsync()
        {
            _logger.LogInformation("AcademicRiskJob — RunDailyRiskAnalysisAsync started at {Time}", DateTime.UtcNow);

            var today = DateTime.UtcNow.Date;

            // Load active offerings (semester has not ended yet)
            var activeOfferings = await _context.SubjectOfferings
                .AsNoTracking()
                .Include(so => so.Subject)
                .Include(so => so.Semester)
                .Where(so => so.Semester.EndDate >= today)
                .Select(so => new
                {
                    OfferingId = so.Id,
                    SubjectId = so.SubjectId,
                    SubjectName = so.Subject.Name,
                    SemesterStart = so.Semester.StartDate,
                    SemesterEnd = so.Semester.EndDate
                })
                .ToListAsync();

            _logger.LogInformation("Found {Count} active offerings to analyse", activeOfferings.Count);

            int totalStudentsAnalysed = 0;
            int totalAtRisk = 0;
            int totalNotifications = 0;

            foreach (var offering in activeOfferings)
            {
                try
                {
                    var (analysed, atRisk, notifications) = await AnalyseOfferingAsync(
                        offering.OfferingId,
                        offering.SubjectId,
                        offering.SubjectName,
                        offering.SemesterStart,
                        offering.SemesterEnd);

                    totalStudentsAnalysed += analysed;
                    totalAtRisk += atRisk;
                    totalNotifications += notifications;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "AcademicRiskJob — failed to analyse offering {OfferingId}", offering.OfferingId);
                }
            }

            _logger.LogInformation(
                "AcademicRiskJob finished — analysed {Analysed} students, {AtRisk} at risk, {Notifications} notifications sent",
                totalStudentsAnalysed, totalAtRisk, totalNotifications);
        }

        public async Task<List<StudentRiskDto>> GetAtRiskStudentsAsync(Ulid offeringId)
        {
            var scores = await _context.AcademicRiskScores
                .AsNoTracking()
                .Include(s => s.Student)
                .Include(s => s.SubjectOffering)
                    .ThenInclude(so => so.Subject)
                .Where(s => s.SubjectOfferingId == offeringId && s.RiskLevel >= RiskLevel.Medium)
                .OrderByDescending(s => s.RiskLevel)
                .ToListAsync();

            return scores.Select(s => new StudentRiskDto(
                s.StudentId.ToString(),
                s.Student.FullName,
                s.SubjectOffering.Subject.Name,
                s.AttendancePercent,
                s.AverageGrade,
                s.RiskLevel.ToString(),
                s.AiRecommendation)).ToList();
        }

        // ── Private helpers ──────────────────────────────────────────────────

        private async Task<(int analysed, int atRisk, int notifications)> AnalyseOfferingAsync(
            Ulid offeringId,
            Ulid subjectId,
            string subjectName,
            DateTime semesterStart,
            DateTime semesterEnd)
        {
            // --- 1. Enrolled students ---
            var enrollments = await _context.Enrollments
                .AsNoTracking()
                .Include(e => e.Student)
                    .ThenInclude(s => s.SystemUser)
                .Where(e => e.SubjectOfferingId == offeringId && e.IsActive)
                .ToListAsync();

            if (enrollments.Count == 0) return (0, 0, 0);

            // --- 2. All grades for this offering ---
            var gradesLookup = await _context.StudentGrades
                .AsNoTracking()
                .Where(g => g.SubjectOfferingId == offeringId)
                .GroupBy(g => g.StudentId)
                .Select(g => new { StudentId = g.Key, AvgScore = g.Average(x => x.FinalScore) })
                .ToListAsync();

            var gradesByStudent = gradesLookup.ToDictionary(g => g.StudentId, g => g.AvgScore);

            int analysed = 0, atRisk = 0, notifications = 0;

            foreach (var enrollment in enrollments)
            {
                try
                {
                    var student = enrollment.Student;
                    var studentId = student.Id;

                    // --- 5. Compute metrics ---
                    double averageGrade = gradesByStudent.TryGetValue(studentId, out var grade) ? grade : 0.0;

                    // --- 6. Determine risk level (grade-based only) ---
                    var riskLevel = DetermineRiskLevel(averageGrade);

                    analysed++;

                    if (riskLevel < RiskLevel.Medium)
                        continue; // Low risk — no action needed

                    atRisk++;

                    // --- 7. Generate recommendation ---
                    var riskDto = new StudentRiskDto(
                        studentId.ToString(),
                        student.FullName,
                        subjectName,
                        0,
                        averageGrade,
                        riskLevel.ToString(),
                        string.Empty);

                    string recommendation = GenerateRecommendation(riskDto);

                    // --- 8. Upsert AcademicRiskScore ---
                    await UpsertRiskScoreAsync(
                        studentId, offeringId, 0, averageGrade, riskLevel, recommendation);

                    // --- 9. Send notification ---
                    var notifSent = await SendRiskNotificationAsync(
                        student.SystemUserId, student.FullName, subjectName,
                        0, averageGrade, riskLevel);

                    if (notifSent) notifications++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "AcademicRiskJob — failed to process student {StudentId} in offering {OfferingId}",
                        enrollment.StudentId, offeringId);
                }
            }

            return (analysed, atRisk, notifications);
        }

        private static RiskLevel DetermineRiskLevel(double averageGrade)
        {
            if (averageGrade < 40) return RiskLevel.Critical;
            if (averageGrade < 50) return RiskLevel.High;
            if (averageGrade < 60) return RiskLevel.Medium;
            return RiskLevel.Low;
        }

        private static string GenerateRecommendation(StudentRiskDto risk) => risk.RiskLevel switch
        {
            "Critical" => "مطلوب تحسين عاجل: احضر جميع المحاضرات القادمة وراجع المادة من البداية",
            "High"     => "راجع فصول المادة الأساسية وتواصل مع الدكتور",
            "Medium"   => "خصص وقتاً إضافياً للمذاكرة وحل التمارين",
            _          => "استمر في أدائك الجيد"
        };

        private async Task UpsertRiskScoreAsync(
            Ulid studentId, Ulid offeringId,
            double attendancePercent, double averageGrade,
            RiskLevel riskLevel, string recommendation)
        {
            var existing = await _context.AcademicRiskScores
                .FirstOrDefaultAsync(r => r.StudentId == studentId && r.SubjectOfferingId == offeringId);

            if (existing == null)
            {
                _context.AcademicRiskScores.Add(new AcademicRiskScore
                {
                    StudentId         = studentId,
                    SubjectOfferingId = offeringId,
                    AttendancePercent = attendancePercent,
                    AverageGrade      = averageGrade,
                    RiskLevel         = riskLevel,
                    AiRecommendation  = recommendation,
                    AnalyzedAt        = DateTime.UtcNow
                });
            }
            else
            {
                existing.AttendancePercent = attendancePercent;
                existing.AverageGrade      = averageGrade;
                existing.RiskLevel         = riskLevel;
                existing.AiRecommendation  = recommendation;
                existing.AnalyzedAt        = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
        }

        private async Task<bool> SendRiskNotificationAsync(
            Ulid systemUserId, string studentName, string subjectName,
            double attendancePercent, double averageGrade, RiskLevel riskLevel)
        {
            string title;
            string message;

            // Priority: attendance-based first, then grade-based
            if (riskLevel == RiskLevel.Critical && attendancePercent < 70)
            {
                title   = "⚠️ تحذير حضور حرج";
                message = $"⚠️ تحذير: نسبة حضورك في مادة {subjectName} وصلت {attendancePercent:F1}% — أنت على وشك الحرمان من الامتحان";
            }
            else if (riskLevel == RiskLevel.High && attendancePercent < 75)
            {
                title   = "تنبيه حضور";
                message = $"تنبيه: نسبة حضورك في {subjectName} ({attendancePercent:F1}%) تحتاج تحسين";
            }
            else if (riskLevel == RiskLevel.Critical && averageGrade < 40)
            {
                title   = "⚠️ تحذير درجات حرج";
                message = $"⚠️ درجاتك في {subjectName} ({averageGrade:F1}%) في خطر — مراجعة عاجلة مطلوبة";
            }
            else
            {
                title   = "تنبيه أكاديمي";
                message = $"درجاتك في {subjectName} ({averageGrade:F1}%) أقل من المتوسط — يُنصح بمراجعة المادة";
            }

            try
            {
                await _notifications.SendNotificationAsync(
                    systemUserId,
                    title,
                    message,
                    actionUrl: "/my-grades");

                _logger.LogInformation(
                    "Risk notification sent to user {UserId} — {RiskLevel} in {Subject}",
                    systemUserId, riskLevel, subjectName);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send risk notification to user {UserId}", systemUserId);
                return false;
            }
        }
    }
}
