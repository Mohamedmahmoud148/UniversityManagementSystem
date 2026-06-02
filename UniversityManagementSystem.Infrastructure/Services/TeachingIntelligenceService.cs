using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NUlid;
using UniversityManagementSystem.Core.DTOs.TeachingIntelligence;
using UniversityManagementSystem.Core.Entities;
using UniversityManagementSystem.Core.Interfaces;
using UniversityManagementSystem.Infrastructure.Data;

namespace UniversityManagementSystem.Infrastructure.Services
{
    public class TeachingIntelligenceService(
        AppDbContext context,
        IAiService aiService,
        ILogger<TeachingIntelligenceService> logger) : ITeachingIntelligenceService
    {
        private readonly AppDbContext _context = context;
        private readonly IAiService _aiService = aiService;
        private readonly ILogger<TeachingIntelligenceService> _logger = logger;

        // ── Doctor overview ───────────────────────────────────────────────

        public async Task<List<DoctorOfferingSummaryDto>> GetDoctorOfferingsAsync(Ulid doctorUserId)
        {
            var doctor = await _context.Doctors
                .FirstOrDefaultAsync(d => d.SystemUserId == doctorUserId);
            if (doctor == null) return [];

            var offerings = await _context.SubjectOfferings
                .Where(o => o.DoctorId == doctor.Id && o.DeletedAt == null)
                .Include(o => o.Subject)
                .Include(o => o.Batch)
                .Include(o => o.Group)
                .Include(o => o.Department)
                    .ThenInclude(d => d.College)
                .Include(o => o.Semester)
                .ToListAsync();

            var result = new List<DoctorOfferingSummaryDto>();
            foreach (var offering in offerings)
            {
                var snap = await _context.StudentIntelligenceSnapshots
                    .Where(s => s.SubjectOfferingId == offering.Id && s.DoctorId == doctor.Id)
                    .ToListAsync();

                int total = snap.Count;
                int atRisk = snap.Count(s => s.RiskLevel is RiskLevel.High or RiskLevel.Critical);
                double avgGrade = total > 0 ? snap.Where(s => s.FinalScore.HasValue)
                    .Select(s => s.FinalScore!.Value).DefaultIfEmpty(0).Average() : 0;
                double avgAtt = total > 0 ? snap.Average(s => s.AttendancePercent) : 0;
                double completion = total > 0 ? snap.Average(s => s.AssignmentCompletionRate) : 0;

                string health = ComputeClassHealth(avgGrade, avgAtt, atRisk, total);

                result.Add(new DoctorOfferingSummaryDto(
                    OfferingId: offering.Id.ToString(),
                    SubjectName: offering.Subject?.Name ?? "",
                    SubjectCode: offering.Subject?.Code ?? "",
                    BatchName: offering.Batch?.Name ?? "",
                    GroupName: offering.Group?.Name ?? "",
                    DepartmentName: offering.Department?.Name ?? "",
                    CollegeName: offering.Department?.College?.Name ?? "",
                    SemesterName: offering.Semester?.Name ?? "",
                    TotalStudents: total,
                    AtRiskCount: atRisk,
                    AverageGrade: Math.Round(avgGrade, 1),
                    AverageAttendance: Math.Round(avgAtt, 1),
                    AssignmentCompletionRate: Math.Round(completion, 1),
                    OverallHealth: health
                ));
            }
            return result.OrderByDescending(o => o.AtRiskCount).ToList();
        }

        // ── Full dashboard ────────────────────────────────────────────────

        public async Task<TeachingDashboardDto> GetDashboardAsync(Ulid doctorUserId)
        {
            var doctor = await _context.Doctors
                .FirstOrDefaultAsync(d => d.SystemUserId == doctorUserId);
            if (doctor == null)
                return EmptyDashboard();

            var offerings = await GetDoctorOfferingsAsync(doctorUserId);
            var allSnapshots = await _context.StudentIntelligenceSnapshots
                .Where(s => s.DoctorId == doctor.Id)
                .ToListAsync();

            var stats = new DashboardStatsDto(
                TotalStudents:             allSnapshots.Select(s => s.StudentId).Distinct().Count(),
                TotalOfferings:            offerings.Count,
                CriticalRiskCount:         allSnapshots.Count(s => s.RiskLevel == RiskLevel.Critical),
                HighRiskCount:             allSnapshots.Count(s => s.RiskLevel == RiskLevel.High),
                MediumRiskCount:           allSnapshots.Count(s => s.RiskLevel == RiskLevel.Medium),
                OverallAverageGrade:       allSnapshots.Any(s => s.FinalScore.HasValue)
                    ? Math.Round(allSnapshots.Where(s => s.FinalScore.HasValue).Average(s => s.FinalScore!.Value), 1) : 0,
                OverallAttendanceRate:     allSnapshots.Any() ? Math.Round(allSnapshots.Average(s => s.AttendancePercent), 1) : 0,
                OverallAssignmentCompletion: allSnapshots.Any() ? Math.Round(allSnapshots.Average(s => s.AssignmentCompletionRate), 1) : 0,
                MostImprovedCount:         allSnapshots.Count(s => s.OverallTrend == "improving"),
                DecliningCount:            allSnapshots.Count(s => s.OverallTrend == "declining")
            );

            var atRisk = allSnapshots
                .Where(s => s.RiskLevel is RiskLevel.High or RiskLevel.Critical)
                .OrderByDescending(s => s.RiskScore)
                .Take(20)
                .Select(MapToStudentDto)
                .ToList();

            var weakTopics = await GetAllWeakTopicsAsync(doctor.Id);
            var comparisons = await GetClassComparisonsForDashboardAsync(doctor.Id);
            var alerts = await GetAlertsAsync(doctorUserId, unreadOnly: true);
            var aiRecs = await GenerateDashboardRecommendationsAsync(stats, weakTopics, atRisk);

            return new TeachingDashboardDto(
                Offerings: offerings,
                OverallStats: stats,
                AtRiskStudents: atRisk,
                WeakTopics: weakTopics.Take(8).ToList(),
                ClassComparisons: comparisons,
                RecentAlerts: alerts.Take(10).ToList(),
                AiRecommendations: aiRecs
            );
        }

        // ── Class analytics ───────────────────────────────────────────────

        public async Task<ClassIntelligenceDto> GetClassIntelligenceAsync(
            Ulid subjectOfferingId, Ulid doctorUserId, TeachingQueryFilter filter)
        {
            var doctor = await GetDoctorAsync(doctorUserId);
            var offering = await GetVerifiedOfferingAsync(subjectOfferingId, doctor?.Id);

            var query = _context.StudentIntelligenceSnapshots
                .Where(s => s.SubjectOfferingId == subjectOfferingId);

            // Apply filters
            if (!string.IsNullOrEmpty(filter.RiskLevel))
            {
                if (Enum.TryParse<RiskLevel>(filter.RiskLevel, ignoreCase: true, out var rl))
                    query = query.Where(s => s.RiskLevel == rl);
            }
            if (filter.AtRiskOnly)
                query = query.Where(s => s.RiskLevel == RiskLevel.High || s.RiskLevel == RiskLevel.Critical);
            if (!string.IsNullOrEmpty(filter.Trend))
                query = query.Where(s => s.OverallTrend == filter.Trend);

            // Sort
            query = filter.SortBy.ToLower() switch
            {
                "name"       => filter.SortDir == "desc" ? query.OrderByDescending(s => s.StudentName) : query.OrderBy(s => s.StudentName),
                "grade"      => filter.SortDir == "desc" ? query.OrderByDescending(s => s.FinalScore) : query.OrderBy(s => s.FinalScore),
                "attendance" => filter.SortDir == "desc" ? query.OrderByDescending(s => s.AttendancePercent) : query.OrderBy(s => s.AttendancePercent),
                _            => query.OrderByDescending(s => s.RiskScore),
            };

            var snapshots = await query.ToListAsync();
            var paged = snapshots
                .Skip((filter.Page - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .Select(MapToStudentDto)
                .ToList();

            int total = snapshots.Count;
            var passing = snapshots.Count(s => (s.FinalScore ?? 0) >= 50);
            var failing  = total - passing;

            var gradeGroups = new[]
            {
                new GradeDistributionDto("A (85+)",   snapshots.Count(s => (s.FinalScore ?? 0) >= 85), 0),
                new GradeDistributionDto("B (70-84)", snapshots.Count(s => (s.FinalScore ?? 0) is >= 70 and < 85), 0),
                new GradeDistributionDto("C (60-69)", snapshots.Count(s => (s.FinalScore ?? 0) is >= 60 and < 70), 0),
                new GradeDistributionDto("D (50-59)", snapshots.Count(s => (s.FinalScore ?? 0) is >= 50 and < 60), 0),
                new GradeDistributionDto("F (<50)",   snapshots.Count(s => (s.FinalScore ?? 0) < 50), 0),
            };
            var gradeDistribution = gradeGroups.Select(g =>
                g with { Percentage = total > 0 ? Math.Round((double)g.Count / total * 100, 1) : 0 }
            ).ToList();

            double avgGrade = total > 0 && snapshots.Any(s => s.FinalScore.HasValue)
                ? Math.Round(snapshots.Where(s => s.FinalScore.HasValue).Average(s => s.FinalScore!.Value), 1) : 0;
            double avgAtt = total > 0 ? Math.Round(snapshots.Average(s => s.AttendancePercent), 1) : 0;
            double avgCompletion = total > 0 ? Math.Round(snapshots.Average(s => s.AssignmentCompletionRate), 1) : 0;
            double avgQuiz = snapshots.Any(s => s.AvgQuizScore.HasValue)
                ? Math.Round(snapshots.Where(s => s.AvgQuizScore.HasValue).Average(s => s.AvgQuizScore!.Value), 1) : 0;
            double avgExam = snapshots.Any(s => s.AvgExamScore.HasValue)
                ? Math.Round(snapshots.Where(s => s.AvgExamScore.HasValue).Average(s => s.AvgExamScore!.Value), 1) : 0;
            double avgEngagement = total > 0 ? Math.Round(snapshots.Average(s => s.EngagementScore), 1) : 0;

            int atRisk = snapshots.Count(s => s.RiskLevel is RiskLevel.High or RiskLevel.Critical);
            int critical = snapshots.Count(s => s.RiskLevel == RiskLevel.Critical);
            string health = ComputeClassHealth(avgGrade, avgAtt, atRisk, total);

            int improving = snapshots.Count(s => s.OverallTrend == "improving");
            int declining  = snapshots.Count(s => s.OverallTrend == "declining");
            string healthTrend = improving > declining ? "improving" : declining > improving ? "declining" : "stable";

            var weakTopics = await GetOfferingWeakTopicsAsync(subjectOfferingId);
            var perfTrend  = await BuildPerformanceTrendAsync(subjectOfferingId);

            return new ClassIntelligenceDto(
                OfferingId: subjectOfferingId.ToString(),
                SubjectName: offering?.Subject?.Name ?? "",
                BatchName: offering?.Batch?.Name ?? "",
                GroupName: offering?.Group?.Name ?? "",
                TotalStudents: total,
                AverageGrade: avgGrade,
                AverageAttendance: avgAtt,
                AssignmentCompletionRate: avgCompletion,
                AverageQuizScore: avgQuiz > 0 ? avgQuiz : null,
                AverageExamScore: avgExam > 0 ? avgExam : null,
                AtRiskCount: atRisk,
                CriticalCount: critical,
                PassingCount: passing,
                FailingCount: failing,
                EngagementAverage: avgEngagement,
                ClassHealth: health,
                HealthTrend: healthTrend,
                Students: paged,
                WeakTopics: weakTopics,
                GradeDistribution: gradeDistribution,
                PerformanceTrend: perfTrend
            );
        }

        // ── Student analytics ─────────────────────────────────────────────

        public async Task<List<StudentIntelligenceDto>> GetStudentsAsync(
            Ulid subjectOfferingId, Ulid doctorUserId, TeachingQueryFilter filter)
        {
            var result = await GetClassIntelligenceAsync(subjectOfferingId, doctorUserId, filter);
            return result.Students;
        }

        public async Task<StudentIntelligenceDto?> GetStudentAnalyticsAsync(
            Ulid studentId, Ulid subjectOfferingId, Ulid doctorUserId)
        {
            var doctor = await GetDoctorAsync(doctorUserId);
            var snap = await _context.StudentIntelligenceSnapshots
                .FirstOrDefaultAsync(s =>
                    s.StudentId == studentId &&
                    s.SubjectOfferingId == subjectOfferingId &&
                    s.DoctorId == doctor!.Id);
            return snap == null ? null : MapToStudentDto(snap);
        }

        public async Task<List<ClassComparisonDto>> GetClassComparisonAsync(
            string subjectName, Ulid doctorUserId)
        {
            var doctor = await GetDoctorAsync(doctorUserId);
            if (doctor == null) return [];
            var all = await GetClassComparisonsForDashboardAsync(doctor.Id);
            if (string.IsNullOrEmpty(subjectName)) return all;
            return all.Where(c => c.Label.Contains(subjectName, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        public async Task<List<StudentIntelligenceDto>> GetAtRiskStudentsAsync(
            Ulid doctorUserId, string? minRiskLevel = "medium")
        {
            var doctor = await GetDoctorAsync(doctorUserId);
            if (doctor == null) return [];

            var minLevel = Enum.TryParse<RiskLevel>(minRiskLevel, ignoreCase: true, out var rl)
                ? rl : RiskLevel.Medium;

            return await _context.StudentIntelligenceSnapshots
                .Where(s => s.DoctorId == doctor.Id && s.RiskLevel >= minLevel)
                .OrderByDescending(s => s.RiskScore)
                .Take(100)
                .Select(s => MapToStudentDto(s))
                .ToListAsync();
        }

        public async Task<List<StudentIntelligenceDto>> GetMostImprovedAsync(
            Ulid subjectOfferingId, Ulid doctorUserId, int limit = 10)
        {
            return await _context.StudentIntelligenceSnapshots
                .Where(s => s.SubjectOfferingId == subjectOfferingId
                         && s.OverallTrend == "improving")
                .OrderByDescending(s => s.GradeTrend + s.AttendanceTrend)
                .Take(limit)
                .Select(s => MapToStudentDto(s))
                .ToListAsync();
        }

        // ── Topic analytics ───────────────────────────────────────────────

        public async Task<TopicAnalyticsDto> GetTopicAnalyticsAsync(
            Ulid subjectOfferingId, Ulid doctorUserId)
        {
            var offering = await _context.SubjectOfferings
                .Include(o => o.Subject)
                .FirstOrDefaultAsync(o => o.Id == subjectOfferingId);

            var weak   = await GetOfferingWeakTopicsAsync(subjectOfferingId);
            var strong = await GetOfferingStrongTopicsAsync(subjectOfferingId);
            var qPerf  = await GetQuestionPerformanceAsync(subjectOfferingId);

            return new TopicAnalyticsDto(
                OfferingId: subjectOfferingId.ToString(),
                SubjectName: offering?.Subject?.Name ?? "",
                WeakTopics: weak,
                StrongTopics: strong,
                QuestionBreakdown: qPerf
            );
        }

        // ── Alerts ────────────────────────────────────────────────────────

        public async Task<List<TeachingAlertDto>> GetAlertsAsync(
            Ulid doctorUserId, bool unreadOnly = false)
        {
            var query = _context.AiInsights
                .Where(i => i.UserId == doctorUserId
                         && i.DeletedAt == null
                         && (i.ExpiresAt == null || i.ExpiresAt > DateTime.UtcNow)
                         && i.InsightType >= InsightType.TeachingRecommendation);

            if (unreadOnly)
                query = query.Where(i => !i.IsAcknowledged);

            var insights = await query
                .OrderByDescending(i => i.CreatedAt)
                .Take(50)
                .ToListAsync();

            return insights.Select(i => new TeachingAlertDto(
                AlertId: i.Id.ToString(),
                AlertType: MapInsightTypeToAlertType(i.InsightType),
                Severity: i.Priority switch
                {
                    InsightPriority.Urgent => "critical",
                    InsightPriority.High   => "warning",
                    _                      => "info",
                },
                Title: i.Title,
                Message: i.Message,
                StudentId: null,
                StudentName: null,
                OfferingId: i.DataPayload.Contains("offeringId")
                    ? TryExtractJson(i.DataPayload, "offeringId") : null,
                IsRead: i.IsAcknowledged,
                CreatedAt: i.CreatedAt
            )).ToList();
        }

        public async Task MarkAlertReadAsync(Ulid alertId, Ulid doctorUserId)
        {
            var insight = await _context.AiInsights
                .FirstOrDefaultAsync(i => i.Id == alertId && i.UserId == doctorUserId)
                ?? throw new KeyNotFoundException("Alert not found.");
            insight.IsAcknowledged = true;
            await _context.SaveChangesAsync();
        }

        // ── AI Insights ───────────────────────────────────────────────────

        public async Task<List<TeachingInsightDto>> GetAiInsightsAsync(
            Ulid doctorUserId, string? offeringId = null)
        {
            var doctor = await GetDoctorAsync(doctorUserId);
            if (doctor == null) return [];

            // Gather analytics for AI prompt
            var stats = await GetDashboardAsync(doctorUserId);
            var insights = await GenerateTeachingInsightsAsync(stats, doctor.FullName);
            return insights;
        }

        // ── Excel Export ──────────────────────────────────────────────────

        public async Task<ExcelExportMetaDto> GetStudentExportDataAsync(
            Ulid subjectOfferingId, Ulid doctorUserId)
        {
            var doctor = await GetDoctorAsync(doctorUserId);
            var offering = await _context.SubjectOfferings
                .Include(o => o.Subject)
                .Include(o => o.Batch)
                .Include(o => o.Group)
                .FirstOrDefaultAsync(o => o.Id == subjectOfferingId);

            var snapshots = await _context.StudentIntelligenceSnapshots
                .Where(s => s.SubjectOfferingId == subjectOfferingId)
                .OrderBy(s => s.StudentName)
                .ToListAsync();

            var rows = snapshots.Select(s => new StudentExcelRowDto(
                UniversityId: s.StudentUniversityId,
                StudentName: s.StudentName,
                BatchName: s.BatchName,
                GroupName: s.GroupName,
                DepartmentName: s.DepartmentName,
                CollegeName: s.CollegeName,
                SubjectName: s.SubjectName,
                FinalScore: s.FinalScore.HasValue ? Math.Round(s.FinalScore.Value, 1) : null,
                MidtermScore: s.MidtermScore.HasValue ? Math.Round(s.MidtermScore.Value, 1) : null,
                CourseworkScore: s.CourseworkScore.HasValue ? Math.Round(s.CourseworkScore.Value, 1) : null,
                FinalExamScore: s.FinalExamScore.HasValue ? Math.Round(s.FinalExamScore.Value, 1) : null,
                GradeCategory: s.FinalScore.HasValue
                    ? (s.FinalScore.Value >= 50 ? "Pass" : "Fail") : "Pending",
                TotalSessions: s.TotalSessions,
                AttendedSessions: s.AttendedSessions,
                AttendancePercent: Math.Round(s.AttendancePercent, 1),
                TotalAssignments: s.TotalAssignments,
                SubmittedAssignments: s.SubmittedAssignments,
                MissingAssignments: s.MissingAssignments,
                AssignmentCompletionRate: Math.Round(s.AssignmentCompletionRate, 1),
                TotalExams: s.TotalExams,
                AvgExamScore: s.AvgExamScore.HasValue ? Math.Round(s.AvgExamScore.Value, 1) : null,
                AvgQuizScore: s.AvgQuizScore.HasValue ? Math.Round(s.AvgQuizScore.Value, 1) : null,
                RiskScore: Math.Round(s.RiskScore, 1),
                RiskLevel: s.RiskLevel.ToString(),
                RiskFactors: s.RiskFactors,
                AiSessions: s.AiSessionCount,
                StudyMinutes: s.AiStudyMinutes,
                StreakDays: s.LearningStreakDays
            )).ToList();

            return new ExcelExportMetaDto(
                ExportTitle: $"{offering?.Subject?.Name ?? "Subject"} — Student Report",
                GeneratedAt: DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm UTC"),
                DoctorName: doctor?.FullName ?? "",
                SubjectName: offering?.Subject?.Name ?? "",
                BatchName: offering?.Batch?.Name ?? "",
                GroupName: offering?.Group?.Name ?? "",
                TotalRows: rows.Count,
                Rows: rows
            );
        }

        public async Task<ExcelExportMetaDto> GetBatchExportDataAsync(
            Ulid batchId, Ulid doctorUserId, Ulid? subjectOfferingId = null)
        {
            var doctor = await GetDoctorAsync(doctorUserId);
            if (doctor == null)
                return new ExcelExportMetaDto("", "", "", "", "", "", 0, []);

            var query = _context.StudentIntelligenceSnapshots
                .Where(s => s.DoctorId == doctor.Id && s.BatchName != "");

            // Filter by batch name (denormalized)
            var batch = await _context.Batches.FirstOrDefaultAsync(b => b.Id == batchId);
            if (batch != null)
                query = query.Where(s => s.BatchName == batch.Name);

            if (subjectOfferingId.HasValue)
                query = query.Where(s => s.SubjectOfferingId == subjectOfferingId.Value);

            var snapshots = await query.OrderBy(s => s.StudentName).ToListAsync();

            var rows = snapshots.Select(s => new StudentExcelRowDto(
                UniversityId: s.StudentUniversityId,
                StudentName: s.StudentName,
                BatchName: s.BatchName,
                GroupName: s.GroupName,
                DepartmentName: s.DepartmentName,
                CollegeName: s.CollegeName,
                SubjectName: s.SubjectName,
                FinalScore: s.FinalScore.HasValue ? Math.Round(s.FinalScore.Value, 1) : null,
                MidtermScore: s.MidtermScore.HasValue ? Math.Round(s.MidtermScore.Value, 1) : null,
                CourseworkScore: s.CourseworkScore.HasValue ? Math.Round(s.CourseworkScore.Value, 1) : null,
                FinalExamScore: s.FinalExamScore.HasValue ? Math.Round(s.FinalExamScore.Value, 1) : null,
                GradeCategory: s.FinalScore.HasValue
                    ? (s.FinalScore.Value >= 50 ? "Pass" : "Fail") : "Pending",
                TotalSessions: s.TotalSessions,
                AttendedSessions: s.AttendedSessions,
                AttendancePercent: Math.Round(s.AttendancePercent, 1),
                TotalAssignments: s.TotalAssignments,
                SubmittedAssignments: s.SubmittedAssignments,
                MissingAssignments: s.MissingAssignments,
                AssignmentCompletionRate: Math.Round(s.AssignmentCompletionRate, 1),
                TotalExams: s.TotalExams,
                AvgExamScore: s.AvgExamScore.HasValue ? Math.Round(s.AvgExamScore.Value, 1) : null,
                AvgQuizScore: s.AvgQuizScore.HasValue ? Math.Round(s.AvgQuizScore.Value, 1) : null,
                RiskScore: Math.Round(s.RiskScore, 1),
                RiskLevel: s.RiskLevel.ToString(),
                RiskFactors: s.RiskFactors,
                AiSessions: s.AiSessionCount,
                StudyMinutes: s.AiStudyMinutes,
                StreakDays: s.LearningStreakDays
            )).ToList();

            return new ExcelExportMetaDto(
                ExportTitle: $"Batch Report — {batch?.Name ?? "All Batches"}",
                GeneratedAt: DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm UTC"),
                DoctorName: doctor?.FullName ?? "",
                SubjectName: subjectOfferingId.HasValue ? snapshots.FirstOrDefault()?.SubjectName ?? "" : "All Subjects",
                BatchName: batch?.Name ?? "",
                GroupName: "",
                TotalRows: rows.Count,
                Rows: rows
            );
        }

        // ── Snapshot computation (called by background job) ───────────────

        public async Task RefreshSnapshotAsync(Ulid subjectOfferingId)
        {
            var offering = await _context.SubjectOfferings
                .Include(o => o.Subject)
                .Include(o => o.Batch)
                .Include(o => o.Group)
                .Include(o => o.Department)
                    .ThenInclude(d => d!.College)
                .FirstOrDefaultAsync(o => o.Id == subjectOfferingId);

            if (offering == null) return;

            var enrollments = await _context.Enrollments
                .Where(e => e.SubjectOfferingId == subjectOfferingId && e.IsActive)
                .Include(e => e.Student)
                    .ThenInclude(s => s.SystemUser)
                .ToListAsync();

            var grades = await _context.StudentGrades
                .Where(g => g.SubjectOfferingId == subjectOfferingId)
                .ToDictionaryAsync(g => g.StudentId);

            var assignments = await _context.Assignments
                .Where(a => a.SubjectOfferingId == subjectOfferingId && a.DeletedAt == null)
                .Select(a => a.Id)
                .ToListAsync();

            var allSubmissions = await _context.AssignmentSubmissions
                .Where(s => assignments.Contains(s.AssignmentId))
                .ToListAsync();

            // Attendance: sessions for this subject (AttendanceSession.SubjectId → not OfferingId)
            var subjectId = offering.SubjectId;
            var attendanceSessions = await _context.AttendanceSessions
                .Where(s => s.SubjectId == subjectId && s.DeletedAt == null)
                .Select(s => s.Id)
                .ToListAsync();

            var allAttendances = await _context.StudentAttendances
                .Where(a => attendanceSessions.Contains(a.AttendanceSessionId))
                .ToListAsync();

            // Exams
            var exams = await _context.Exams
                .Where(e => e.SubjectOfferingId == subjectOfferingId && e.Status != ExamStatus.Draft)
                .ToListAsync();
            var examIds = exams.Select(e => e.Id).ToList();

            var allExamSubmissions = await _context.ExamSubmissions
                .Where(s => examIds.Contains(s.ExamId))
                .ToListAsync();

            // Companion profiles
            var studentUserIds = enrollments
                .Select(e => e.Student?.SystemUserId.ToString())
                .Where(id => id != null)
                .ToList();

            var companionProfiles = await _context.AiCompanionProfiles
                .Where(p => studentUserIds.Contains(p.UserId.ToString()))
                .ToListAsync();
            var profileByUserId = companionProfiles.ToDictionary(p => p.UserId.ToString());

            // Previous snapshots for trend computation
            var prevSnapshots = await _context.StudentIntelligenceSnapshots
                .Where(s => s.SubjectOfferingId == subjectOfferingId)
                .ToDictionaryAsync(s => s.StudentId);

            var now = DateTime.UtcNow;

            foreach (var enrollment in enrollments)
            {
                var student = enrollment.Student;
                if (student == null) continue;

                // ── Grade ────────────────────────────────────────────────
                grades.TryGetValue(student.Id, out var grade);
                double? finalScore      = grade?.FinalScore;
                double? midtermScore    = grade?.MidtermScore;
                double? courseworkScore = grade?.CourseworkScore;
                double? finalExamScore  = grade?.FinalExamScore;
                double gradeScore = finalScore.HasValue
                    ? Math.Min(100, finalScore.Value) : 0;

                // ── Attendance ───────────────────────────────────────────
                var studentAtt = allAttendances.Where(a => a.StudentId == student.Id).ToList();
                int totalSessions   = attendanceSessions.Count;
                int attendedSessions = studentAtt.Count(a => a.IsPresent);
                double attPercent = totalSessions > 0
                    ? Math.Round((double)attendedSessions / totalSessions * 100, 1) : 0;
                double attScore = attPercent >= 90 ? 100
                    : attPercent >= 75 ? 75
                    : attPercent >= 60 ? 50
                    : attPercent >= 50 ? 25 : 0;

                // ── Assignments ──────────────────────────────────────────
                int totalAssignments = assignments.Count;
                var studentSubs = allSubmissions.Where(s => s.StudentId == student.Id).ToList();
                int submitted   = studentSubs.Count;
                int late        = studentSubs.Count(s => s.IsLate);
                int missing     = Math.Max(0, totalAssignments - submitted);
                double completion = totalAssignments > 0
                    ? Math.Round((double)submitted / totalAssignments * 100, 1) : 100;
                double? avgAssignmentGrade = studentSubs.Any(s => s.Grade.HasValue)
                    ? studentSubs.Where(s => s.Grade.HasValue).Average(s => s.Grade!.Value) : null;

                // ── Exams ────────────────────────────────────────────────
                int totalExams = exams.Count;
                var studentExamSubs = allExamSubmissions.Where(s => s.StudentId == student.Id).ToList();
                int takenExams = studentExamSubs.Count;

                var quizSubs  = studentExamSubs.Where(s => exams.Any(e => e.Id == s.ExamId && e.Type == ExamType.Quiz)).ToList();
                var examSubsF = studentExamSubs.Where(s => exams.Any(e => e.Id == s.ExamId && e.Type != ExamType.Quiz)).ToList();

                double? avgExamScore = examSubsF.Any(s => s.Score.HasValue)
                    ? examSubsF.Where(s => s.Score.HasValue).Average(s => s.Score!.Value) : null;
                double? avgQuizScore = quizSubs.Any(s => s.Score.HasValue)
                    ? quizSubs.Where(s => s.Score.HasValue).Average(s => s.Score!.Value) : null;

                // ── AI Companion ─────────────────────────────────────────
                profileByUserId.TryGetValue(student.SystemUserId.ToString(), out var profile);
                int aiSessions    = profile?.TotalSessions ?? 0;
                int streakDays    = profile?.CurrentStreakDays ?? 0;
                double engagement = profile?.EngagementScore ?? 0;
                int aiMinutes     = 0; // populated from Redis separately

                // ── Risk Score ───────────────────────────────────────────
                var (riskScore, riskLevel, riskFactors, riskExplanation, recommendedAction) =
                    ComputeRiskScore(
                        gradeScore, attPercent, completion, missing, totalAssignments,
                        takenExams, totalExams, engagement, streakDays
                    );

                // ── Trends ───────────────────────────────────────────────
                double gradeTrend = 0, attTrend = 0;
                string overallTrend = "stable";
                if (prevSnapshots.TryGetValue(student.Id, out var prev))
                {
                    gradeTrend = gradeScore - prev.GradeScore;
                    attTrend   = attPercent - prev.AttendancePercent;
                    overallTrend = (gradeTrend > 5 || attTrend > 5) ? "improving"
                        : (gradeTrend < -5 || attTrend < -5) ? "declining"
                        : "stable";
                }

                // ── Upsert snapshot ──────────────────────────────────────
                if (prevSnapshots.TryGetValue(student.Id, out var existing))
                {
                    UpdateSnapshot(existing, student, offering, grade,
                        totalSessions, attendedSessions, attPercent, attScore,
                        totalAssignments, submitted, late, missing, completion, avgAssignmentGrade,
                        totalExams, takenExams, avgExamScore, avgQuizScore,
                        aiSessions, aiMinutes, streakDays, engagement,
                        riskScore, riskLevel, riskFactors, riskExplanation, recommendedAction,
                        gradeTrend, attTrend, overallTrend, now);
                }
                else
                {
                    var snap = new StudentIntelligenceSnapshot
                    {
                        StudentId          = student.Id,
                        SubjectOfferingId  = subjectOfferingId,
                        DoctorId           = offering.DoctorId,
                        StudentName        = student.FullName,
                        StudentUniversityId= student.UniversityStudentId,
                        BatchName          = offering.Batch?.Name ?? "",
                        GroupName          = offering.Group?.Name ?? "",
                        DepartmentName     = offering.Department?.Name ?? "",
                        CollegeName        = offering.Department?.College?.Name ?? "",
                        SubjectName        = offering.Subject?.Name ?? "",
                    };
                    UpdateSnapshot(snap, student, offering, grade,
                        totalSessions, attendedSessions, attPercent, attScore,
                        totalAssignments, submitted, late, missing, completion, avgAssignmentGrade,
                        totalExams, takenExams, avgExamScore, avgQuizScore,
                        aiSessions, aiMinutes, streakDays, engagement,
                        riskScore, riskLevel, riskFactors, riskExplanation, recommendedAction,
                        0, 0, "stable", now);
                    _context.StudentIntelligenceSnapshots.Add(snap);
                }
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation(
                "TeachingIntelligence: refreshed snapshot for offering {Id} — {Count} students",
                subjectOfferingId, enrollments.Count);
        }

        public async Task RefreshAllDoctorSnapshotsAsync(Ulid doctorUserId)
        {
            var doctor = await GetDoctorAsync(doctorUserId);
            if (doctor == null) return;

            var offeringIds = await _context.SubjectOfferings
                .Where(o => o.DoctorId == doctor.Id && o.DeletedAt == null)
                .Select(o => o.Id)
                .ToListAsync();

            foreach (var id in offeringIds)
            {
                try { await RefreshSnapshotAsync(id); }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "RefreshAllDoctorSnapshotsAsync: failed for offering {Id}", id);
                }
            }
        }

        // ── Risk scoring engine ───────────────────────────────────────────

        private static (double score, RiskLevel level, string factorsJson,
            string explanation, string action)
        ComputeRiskScore(
            double gradeScore, double attPercent, double completionRate,
            int missingAssignments, int totalAssignments,
            int takenExams, int totalExams, double engagement, int streakDays)
        {
            var factors = new List<string>();
            double risk = 0;

            // Grade component (35 pts max)
            double gradeRisk = gradeScore < 40 ? 35
                : gradeScore < 50 ? 25
                : gradeScore < 60 ? 15
                : gradeScore < 70 ? 8 : 0;
            risk += gradeRisk;
            if (gradeRisk >= 15) factors.Add("Low academic grade");

            // Attendance component (30 pts max)
            double attRisk = attPercent < 50 ? 30
                : attPercent < 60 ? 22
                : attPercent < 70 ? 15
                : attPercent < 75 ? 8 : 0;
            risk += attRisk;
            if (attRisk >= 15) factors.Add($"Low attendance ({attPercent:F0}%)");

            // Assignment component (20 pts max)
            double assignRisk = 0;
            if (totalAssignments > 0)
            {
                double missingRate = (double)missingAssignments / totalAssignments;
                assignRisk = missingRate >= 0.6 ? 20
                    : missingRate >= 0.4 ? 14
                    : missingRate >= 0.2 ? 8 : 0;
                risk += assignRisk;
                if (assignRisk >= 8) factors.Add($"Missing {missingAssignments} assignment(s)");
            }

            // Engagement component (15 pts max)
            double engagementRisk = engagement < 10 && streakDays == 0 ? 15
                : engagement < 25 ? 8
                : engagement < 40 ? 4 : 0;
            risk += engagementRisk;
            if (engagementRisk >= 8) factors.Add("Low AI platform engagement");

            risk = Math.Min(100, Math.Round(risk, 1));

            RiskLevel level = risk >= 75 ? RiskLevel.Critical
                : risk >= 55 ? RiskLevel.High
                : risk >= 30 ? RiskLevel.Medium
                : RiskLevel.Low;

            string explanation = BuildRiskExplanation(risk, factors);
            string action = BuildRecommendedAction(level, factors);

            return (risk, level, JsonSerializer.Serialize(factors), explanation, action);
        }

        private static string BuildRiskExplanation(double score, List<string> factors)
        {
            if (!factors.Any()) return "Student is performing well with no significant risks detected.";
            var issues = string.Join(" and ", factors.Take(2).Select(f => f.ToLower()));
            return score >= 75
                ? $"High risk ({score:F0}/100): Student shows critical issues with {issues}. Immediate intervention recommended."
                : score >= 55
                ? $"Risk score {score:F0}/100: Student is struggling with {issues}."
                : $"Moderate risk ({score:F0}/100): Early warning signs detected — {issues}.";
        }

        private static string BuildRecommendedAction(RiskLevel level, List<string> factors)
        {
            if (level == RiskLevel.Critical) return "Schedule immediate office hour meeting. Notify academic advisor.";
            if (level == RiskLevel.High) return "Send personalized support message. Assign remedial exercises.";
            if (factors.Contains("Low attendance (0%)") || factors.Any(f => f.Contains("attendance")))
                return "Follow up on attendance. Check for personal issues.";
            if (factors.Any(f => f.Contains("assignment")))
                return "Send assignment reminder with extension offer.";
            return "Monitor closely. Encourage AI companion usage.";
        }

        // ── Topic analysis helpers ────────────────────────────────────────

        private async Task<List<WeakTopicDto>> GetOfferingWeakTopicsAsync(Ulid subjectOfferingId)
        {
            var examIds = await _context.Exams
                .Where(e => e.SubjectOfferingId == subjectOfferingId)
                .Select(e => e.Id)
                .ToListAsync();

            if (!examIds.Any()) return [];

            // Derive weak topics from exam-level performance (AnswersJson is a blob, not navigable)
            var examStats = await _context.Exams
                .Where(e => examIds.Contains(e.Id))
                .Select(e => new
                {
                    e.Title,
                    e.TotalMarks,
                    SubmissionCount = e.Submissions.Count(s => s.Score.HasValue),
                    AvgScore = e.Submissions.Where(s => s.Score.HasValue)
                                           .Average(s => (double?)s.Score) ?? 0,
                    FailCount = e.Submissions.Count(s => s.Score.HasValue && s.Score < e.TotalMarks * 0.5),
                })
                .Where(e => e.SubmissionCount >= 2)
                .ToListAsync();

            var weakTopics = new List<WeakTopicDto>();
            foreach (var stat in examStats.OrderBy(e => e.AvgScore).Take(10))
            {
                double passThreshold = stat.TotalMarks * 0.5;
                double errorRate = stat.SubmissionCount > 0
                    ? Math.Round((double)stat.FailCount / stat.SubmissionCount * 100, 1) : 0;
                if (errorRate < 40) continue;

                string severity = errorRate >= 80 ? "critical"
                    : errorRate >= 65 ? "high"
                    : errorRate >= 50 ? "medium" : "low";
                string rec = errorRate >= 70
                    ? $"Conduct a revision session for '{stat.Title}' topics before the next exam."
                    : $"Provide additional exercises for '{stat.Title}' topics.";

                weakTopics.Add(new WeakTopicDto(
                    TopicName: stat.Title,
                    SourceType: "exam",
                    AverageScore: Math.Round(stat.AvgScore, 1),
                    ErrorRate: errorRate,
                    AffectedStudents: stat.FailCount,
                    Severity: severity,
                    AiRecommendation: rec
                ));
            }
            return weakTopics;
        }

        private async Task<List<WeakTopicDto>> GetOfferingStrongTopicsAsync(Ulid subjectOfferingId)
        {
            var examIds = await _context.Exams
                .Where(e => e.SubjectOfferingId == subjectOfferingId)
                .Select(e => e.Id)
                .ToListAsync();
            if (!examIds.Any()) return [];

            var examStats = await _context.Exams
                .Where(e => examIds.Contains(e.Id))
                .Select(e => new
                {
                    e.Title,
                    e.TotalMarks,
                    SubmissionCount = e.Submissions.Count(s => s.Score.HasValue),
                    AvgScore = e.Submissions.Where(s => s.Score.HasValue)
                                           .Average(s => (double?)s.Score) ?? 0,
                })
                .Where(e => e.SubmissionCount >= 2)
                .ToListAsync();

            return examStats
                .Where(e => e.TotalMarks > 0 && e.AvgScore / e.TotalMarks >= 0.75)
                .Select(e => new WeakTopicDto(
                    TopicName: e.Title,
                    SourceType: "exam",
                    AverageScore: Math.Round(e.AvgScore, 1),
                    ErrorRate: Math.Round((1 - e.AvgScore / e.TotalMarks) * 100, 1),
                    AffectedStudents: 0,
                    Severity: "low",
                    AiRecommendation: "Students are performing well on this exam."
                ))
                .Take(5)
                .ToList();
        }

        private async Task<List<QuestionPerformanceDto>> GetQuestionPerformanceAsync(Ulid subjectOfferingId)
        {
            // ExamQuestion.Topic does not exist — return exam-level stats instead
            var examIds = await _context.Exams
                .Where(e => e.SubjectOfferingId == subjectOfferingId)
                .Select(e => e.Id)
                .ToListAsync();
            if (!examIds.Any()) return [];

            var raw = await _context.Exams
                .Where(e => examIds.Contains(e.Id))
                .Select(e => new
                {
                    e.Title,
                    TypeStr  = e.Type.ToString(),
                    HasSubs  = e.Submissions.Any(s => s.Score.HasValue),
                    PassCount = e.Submissions.Count(s => s.Score.HasValue && s.Score >= e.TotalMarks * 0.5),
                    SubCount  = e.Submissions.Count(s => s.Score.HasValue),
                    AvgScore  = e.Submissions.Any(s => s.Score.HasValue)
                        ? e.Submissions.Where(s => s.Score.HasValue).Average(s => (double)s.Score!.Value)
                        : 0.0,
                })
                .Where(e => e.SubCount >= 2)
                .OrderBy(e => e.HasSubs ? e.AvgScore : 100)
                .Take(20)
                .ToListAsync();

            return raw.Select(e => new QuestionPerformanceDto(
                QuestionText: e.Title,
                Topic: e.TypeStr,
                CorrectRate: e.SubCount > 0 ? Math.Round((double)e.PassCount / e.SubCount * 100, 1) : 0,
                AverageScore: Math.Round(e.AvgScore, 1),
                TotalAttempts: e.SubCount
            )).ToList();
        }

        private async Task<List<WeakTopicDto>> GetAllWeakTopicsAsync(Ulid doctorId)
        {
            var offeringIds = await _context.SubjectOfferings
                .Where(o => o.DoctorId == doctorId && o.DeletedAt == null)
                .Select(o => o.Id)
                .ToListAsync();

            var allWeak = new List<WeakTopicDto>();
            foreach (var id in offeringIds.Take(5))
            {
                var weak = await GetOfferingWeakTopicsAsync(id);
                allWeak.AddRange(weak);
            }
            return allWeak
                .GroupBy(t => t.TopicName)
                .Select(g => g.OrderByDescending(t => t.ErrorRate).First())
                .OrderByDescending(t => t.ErrorRate)
                .Take(10)
                .ToList();
        }

        private async Task<List<PerformanceTrendPointDto>> BuildPerformanceTrendAsync(
            Ulid subjectOfferingId)
        {
            var exams = await _context.Exams
                .Where(e => e.SubjectOfferingId == subjectOfferingId
                         && e.Status == ExamStatus.Published)
                .OrderBy(e => e.StartTime)
                .Take(6)
                .ToListAsync();

            var result = new List<PerformanceTrendPointDto>();
            foreach (var exam in exams)
            {
                var submissions = await _context.ExamSubmissions
                    .Where(s => s.ExamId == exam.Id && s.Score.HasValue)
                    .Select(s => s.Score!.Value)
                    .ToListAsync();

                if (!submissions.Any()) continue;

                double avg = Math.Round(submissions.Average(), 1);
                double passRate = Math.Round(submissions.Count(s => s >= exam.TotalMarks * 0.5) * 100.0 / submissions.Count, 1);
                result.Add(new PerformanceTrendPointDto(exam.Title, avg, passRate));
            }
            return result;
        }

        private async Task<List<ClassComparisonDto>> GetClassComparisonsForDashboardAsync(
            Ulid doctorId)
        {
            var offerings = await _context.SubjectOfferings
                .Where(o => o.DoctorId == doctorId && o.DeletedAt == null)
                .Include(o => o.Subject)
                .Include(o => o.Batch)
                .Include(o => o.Group)
                .ToListAsync();

            var result = new List<ClassComparisonDto>();
            foreach (var offering in offerings.Take(8))
            {
                var snap = await _context.StudentIntelligenceSnapshots
                    .Where(s => s.SubjectOfferingId == offering.Id)
                    .ToListAsync();
                if (!snap.Any()) continue;

                double avg = snap.Where(s => s.FinalScore.HasValue).Select(s => s.FinalScore!.Value).DefaultIfEmpty(0).Average();
                double att = snap.Average(s => s.AttendancePercent);
                double comp = snap.Average(s => s.AssignmentCompletionRate);
                int atRisk = snap.Count(s => s.RiskLevel is RiskLevel.High or RiskLevel.Critical);

                result.Add(new ClassComparisonDto(
                    OfferingId: offering.Id.ToString(),
                    Label: $"{offering.Subject?.Name} — {offering.Batch?.Name}{(offering.Group != null ? " / " + offering.Group.Name : "")}",
                    AverageGrade: Math.Round(avg, 1),
                    AttendanceRate: Math.Round(att, 1),
                    CompletionRate: Math.Round(comp, 1),
                    AtRiskCount: atRisk,
                    Health: ComputeClassHealth(avg, att, atRisk, snap.Count)
                ));
            }
            return result.OrderBy(c => c.AverageGrade).ToList();
        }

        // ── AI recommendations ────────────────────────────────────────────

        private async Task<List<string>> GenerateDashboardRecommendationsAsync(
            DashboardStatsDto stats, List<WeakTopicDto> weakTopics,
            List<StudentIntelligenceDto> atRiskStudents)
        {
            var recs = new List<string>();

            if (stats.CriticalRiskCount > 0)
                recs.Add($"⚠️ {stats.CriticalRiskCount} student(s) are at CRITICAL risk. Schedule intervention meetings immediately.");

            if (stats.OverallAttendanceRate < 70)
                recs.Add($"📉 Overall attendance is {stats.OverallAttendanceRate:F0}% — below the 75% threshold. Consider reviewing attendance policy.");

            if (weakTopics.Any(t => t.Severity is "critical" or "high"))
            {
                var criticalTopic = weakTopics.First(t => t.Severity is "critical" or "high");
                recs.Add($"📚 {criticalTopic.ErrorRate:F0}% of students struggle with '{criticalTopic.TopicName}'. A revision session is recommended.");
            }

            if (stats.OverallAssignmentCompletion < 60)
                recs.Add($"📝 Assignment completion rate is {stats.OverallAssignmentCompletion:F0}%. Send reminders or extend deadlines for struggling students.");

            if (stats.DecliningCount > stats.MostImprovedCount)
                recs.Add("📊 More students are declining than improving. Review teaching pace and difficulty level.");

            if (!recs.Any())
                recs.Add("✅ Your class is performing well! Keep monitoring for early warning signs.");

            // Optional AI enhancement
            try
            {
                var prompt = $"Doctor has {stats.TotalStudents} students, {stats.CriticalRiskCount} critical risk, " +
                             $"attendance {stats.OverallAttendanceRate:F0}%, avg grade {stats.OverallAverageGrade:F0}%. " +
                             "Give 1 specific teaching recommendation in Arabic (Egyptian dialect), max 20 words.";
                var aiRec = await _aiService.SendQuickPromptAsync(prompt);
                if (!string.IsNullOrEmpty(aiRec))
                    recs.Add($"🤖 {aiRec}");
            }
            catch { /* non-fatal */ }

            return recs.Take(6).ToList();
        }

        private async Task<List<TeachingInsightDto>> GenerateTeachingInsightsAsync(
            TeachingDashboardDto dashboard, string doctorName)
        {
            var insights = new List<TeachingInsightDto>();
            var now = DateTime.UtcNow;

            foreach (var offering in dashboard.Offerings.Take(5))
            {
                if (offering.AtRiskCount > 0)
                    insights.Add(new TeachingInsightDto(
                        InsightId: Ulid.NewUlid().ToString(),
                        Type: "risk_escalation",
                        Priority: offering.AtRiskCount >= 5 ? "high" : "medium",
                        Title: $"{offering.AtRiskCount} at-risk students in {offering.SubjectName}",
                        Insight: $"{offering.AtRiskCount} students in {offering.SubjectName} ({offering.BatchName}) need academic support.",
                        Action: "Review student profiles and schedule office hours.",
                        OfferingId: offering.OfferingId,
                        GeneratedAt: now
                    ));

                if (offering.AverageAttendance < 70)
                    insights.Add(new TeachingInsightDto(
                        InsightId: Ulid.NewUlid().ToString(),
                        Type: "attendance_alert",
                        Priority: "warning",
                        Title: $"Low attendance in {offering.SubjectName}",
                        Insight: $"Average attendance in {offering.SubjectName} is {offering.AverageAttendance:F0}%.",
                        Action: "Send attendance reminder to enrolled students.",
                        OfferingId: offering.OfferingId,
                        GeneratedAt: now
                    ));
            }

            foreach (var topic in dashboard.WeakTopics.Take(3))
                insights.Add(new TeachingInsightDto(
                    InsightId: Ulid.NewUlid().ToString(),
                    Type: "weak_topic",
                    Priority: topic.Severity,
                    Title: $"Students struggling with '{topic.TopicName}'",
                    Insight: $"{topic.ErrorRate:F0}% of students have difficulty with '{topic.TopicName}'.",
                    Action: topic.AiRecommendation,
                    OfferingId: null,
                    GeneratedAt: now
                ));

            return insights.Take(10).ToList();
        }

        // ── Static helpers ────────────────────────────────────────────────

        private static void UpdateSnapshot(
            StudentIntelligenceSnapshot snap, Student student, SubjectOffering offering,
            StudentGrade? grade,
            int totalSessions, int attendedSessions, double attPercent, double attScore,
            int totalAssignments, int submitted, int late, int missing,
            double completion, double? avgAssignmentGrade,
            int totalExams, int takenExams, double? avgExamScore, double? avgQuizScore,
            int aiSessions, int aiMinutes, int streakDays, double engagement,
            double riskScore, RiskLevel riskLevel, string riskFactors,
            string riskExplanation, string recommendedAction,
            double gradeTrend, double attTrend, string overallTrend, DateTime now)
        {
            snap.FinalScore            = grade?.FinalScore;
            snap.MidtermScore          = grade?.MidtermScore;
            snap.CourseworkScore       = grade?.CourseworkScore;
            snap.FinalExamScore        = grade?.FinalExamScore;
            snap.GradeScore            = snap.FinalScore ?? 0;
            snap.TotalSessions         = totalSessions;
            snap.AttendedSessions      = attendedSessions;
            snap.AttendancePercent     = attPercent;
            snap.AttendanceScore       = attScore;
            snap.TotalAssignments      = totalAssignments;
            snap.SubmittedAssignments  = submitted;
            snap.LateSubmissions       = late;
            snap.MissingAssignments    = missing;
            snap.AssignmentCompletionRate = completion;
            snap.AvgAssignmentGrade    = avgAssignmentGrade;
            snap.TotalExams            = totalExams;
            snap.TakenExams            = takenExams;
            snap.AvgExamScore          = avgExamScore;
            snap.AvgQuizScore          = avgQuizScore;
            snap.AiSessionCount        = aiSessions;
            snap.AiStudyMinutes        = aiMinutes;
            snap.LearningStreakDays     = streakDays;
            snap.EngagementScore       = engagement;
            snap.RiskScore             = riskScore;
            snap.RiskLevel             = riskLevel;
            snap.RiskFactors           = riskFactors;
            snap.RiskExplanation       = riskExplanation;
            snap.RecommendedAction     = recommendedAction;
            snap.GradeTrend            = gradeTrend;
            snap.AttendanceTrend       = attTrend;
            snap.OverallTrend          = overallTrend;
            snap.ComputedAt            = now;
        }

        private static StudentIntelligenceDto MapToStudentDto(StudentIntelligenceSnapshot s)
        {
            var factors = TryParseStringList(s.RiskFactors);
            return new StudentIntelligenceDto(
                StudentId: s.StudentId.ToString(),
                StudentName: s.StudentName,
                StudentUniversityId: s.StudentUniversityId,
                BatchName: s.BatchName,
                GroupName: s.GroupName,
                DepartmentName: s.DepartmentName,
                CollegeName: s.CollegeName,
                FinalScore: s.FinalScore.HasValue ? Math.Round(s.FinalScore.Value, 1) : null,
                MidtermScore: s.MidtermScore.HasValue ? Math.Round(s.MidtermScore.Value, 1) : null,
                AttendancePercent: Math.Round(s.AttendancePercent, 1),
                AssignmentCompletionRate: Math.Round(s.AssignmentCompletionRate, 1),
                AvgExamScore: s.AvgExamScore.HasValue ? Math.Round(s.AvgExamScore.Value, 1) : null,
                AvgQuizScore: s.AvgQuizScore.HasValue ? Math.Round(s.AvgQuizScore.Value, 1) : null,
                AvgAssignmentGrade: s.AvgAssignmentGrade.HasValue ? Math.Round(s.AvgAssignmentGrade.Value, 1) : null,
                LateSubmissions: s.LateSubmissions,
                MissingAssignments: s.MissingAssignments,
                AiSessionCount: s.AiSessionCount,
                AiStudyMinutes: s.AiStudyMinutes,
                LearningStreakDays: s.LearningStreakDays,
                EngagementScore: Math.Round(s.EngagementScore, 1),
                RiskScore: Math.Round(s.RiskScore, 1),
                RiskLevel: s.RiskLevel.ToString(),
                RiskFactors: factors,
                RiskExplanation: s.RiskExplanation,
                RecommendedAction: s.RecommendedAction,
                OverallTrend: s.OverallTrend,
                GradeTrend: Math.Round(s.GradeTrend, 1),
                AttendanceTrend: Math.Round(s.AttendanceTrend, 1),
                ComputedAt: s.ComputedAt
            );
        }

        private static string ComputeClassHealth(double avg, double att, int atRisk, int total)
        {
            if (total == 0) return "unknown";
            double atRiskPct = (double)atRisk / total * 100;
            if (avg >= 70 && att >= 80 && atRiskPct < 10) return "excellent";
            if (avg >= 60 && att >= 70 && atRiskPct < 25) return "good";
            if (avg >= 50 && att >= 60 && atRiskPct < 40) return "concerning";
            return "critical";
        }

        private static string MapInsightTypeToAlertType(InsightType t) => t switch
        {
            InsightType.TeachingRecommendation => "teaching_recommendation",
            InsightType.ClassPerformanceAlert  => "class_performance",
            InsightType.RiskAlert              => "risk_escalation",
            InsightType.WeaknessDetected       => "weak_topic",
            _                                  => "general",
        };

        private static List<string> TryParseStringList(string json)
        {
            try { return System.Text.Json.JsonSerializer.Deserialize<List<string>>(json) ?? []; }
            catch { return []; }
        }

        private static string? TryExtractJson(string json, string key)
        {
            try
            {
                var doc = System.Text.Json.JsonDocument.Parse(json);
                return doc.RootElement.TryGetProperty(key, out var val) ? val.GetString() : null;
            }
            catch { return null; }
        }

        private async Task<Doctor?> GetDoctorAsync(Ulid userId) =>
            await _context.Doctors.FirstOrDefaultAsync(d => d.SystemUserId == userId);

        private async Task<SubjectOffering?> GetVerifiedOfferingAsync(
            Ulid offeringId, Ulid? doctorId)
        {
            return await _context.SubjectOfferings
                .Include(o => o.Subject)
                .Include(o => o.Batch)
                .Include(o => o.Group)
                .Include(o => o.Department)
                    .ThenInclude(d => d!.College)
                .FirstOrDefaultAsync(o => o.Id == offeringId
                    && (doctorId == null || o.DoctorId == doctorId));
        }

        private static TeachingDashboardDto EmptyDashboard() =>
            new([], new DashboardStatsDto(0,0,0,0,0,0,0,0,0,0), [], [], [], [], []);
    }
}
