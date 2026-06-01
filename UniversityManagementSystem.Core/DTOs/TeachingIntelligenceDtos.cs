using System;
using System.Collections.Generic;
using UniversityManagementSystem.Core.Entities;

namespace UniversityManagementSystem.Core.DTOs.TeachingIntelligence
{
    // ── Doctor's offering list ────────────────────────────────────────────────

    public record DoctorOfferingSummaryDto(
        string OfferingId,
        string SubjectName,
        string SubjectCode,
        string BatchName,
        string GroupName,
        string DepartmentName,
        string CollegeName,
        string SemesterName,
        int TotalStudents,
        int AtRiskCount,
        double AverageGrade,
        double AverageAttendance,
        double AssignmentCompletionRate,
        string OverallHealth  // "excellent" | "good" | "concerning" | "critical"
    );

    // ── Full teaching dashboard ───────────────────────────────────────────────

    public record TeachingDashboardDto(
        List<DoctorOfferingSummaryDto> Offerings,
        DashboardStatsDto OverallStats,
        List<StudentIntelligenceDto> AtRiskStudents,
        List<WeakTopicDto> WeakTopics,
        List<ClassComparisonDto> ClassComparisons,
        List<TeachingAlertDto> RecentAlerts,
        List<string> AiRecommendations
    );

    public record DashboardStatsDto(
        int TotalStudents,
        int TotalOfferings,
        int CriticalRiskCount,
        int HighRiskCount,
        int MediumRiskCount,
        double OverallAverageGrade,
        double OverallAttendanceRate,
        double OverallAssignmentCompletion,
        int MostImprovedCount,
        int DecliningCount
    );

    // ── Per-student intelligence card ─────────────────────────────────────────

    public record StudentIntelligenceDto(
        // Identity
        string StudentId,
        string StudentName,
        string StudentUniversityId,
        string BatchName,
        string GroupName,
        string DepartmentName,
        string CollegeName,
        // Metrics
        double? FinalScore,
        double? MidtermScore,
        double AttendancePercent,
        double AssignmentCompletionRate,
        double? AvgExamScore,
        double? AvgQuizScore,
        double? AvgAssignmentGrade,
        int LateSubmissions,
        int MissingAssignments,
        // AI Companion
        int AiSessionCount,
        int AiStudyMinutes,
        int LearningStreakDays,
        double EngagementScore,
        // Risk
        double RiskScore,
        string RiskLevel,
        List<string> RiskFactors,
        string RiskExplanation,
        string RecommendedAction,
        // Trend
        string OverallTrend,
        double GradeTrend,
        double AttendanceTrend,
        DateTime ComputedAt
    );

    // ── Class-level analytics ─────────────────────────────────────────────────

    public record ClassIntelligenceDto(
        string OfferingId,
        string SubjectName,
        string BatchName,
        string GroupName,
        int TotalStudents,
        double AverageGrade,
        double AverageAttendance,
        double AssignmentCompletionRate,
        double? AverageQuizScore,
        double? AverageExamScore,
        int AtRiskCount,
        int CriticalCount,
        int PassingCount,
        int FailingCount,
        double EngagementAverage,
        string ClassHealth,   // excellent | good | concerning | critical
        string HealthTrend,   // improving | stable | declining
        List<StudentIntelligenceDto> Students,
        List<WeakTopicDto> WeakTopics,
        List<GradeDistributionDto> GradeDistribution,
        List<PerformanceTrendPointDto> PerformanceTrend
    );

    public record GradeDistributionDto(
        string Range,   // "A (85+)", "B (70-84)", "C (60-69)", "D (50-59)", "F (<50)"
        int Count,
        double Percentage
    );

    public record PerformanceTrendPointDto(
        string Label,   // exam name or date
        double AverageScore,
        double PassRate
    );

    public record ClassComparisonDto(
        string OfferingId,
        string Label,   // e.g. "G1 - Database", "Batch 2023 - ML"
        double AverageGrade,
        double AttendanceRate,
        double CompletionRate,
        int AtRiskCount,
        string Health
    );

    // ── Topic analytics ───────────────────────────────────────────────────────

    public record WeakTopicDto(
        string TopicName,
        string SourceType,   // "exam" | "quiz" | "assignment"
        double AverageScore,
        double ErrorRate,    // % of students who got wrong
        int AffectedStudents,
        string Severity,     // "low" | "medium" | "high" | "critical"
        string AiRecommendation
    );

    public record TopicAnalyticsDto(
        string OfferingId,
        string SubjectName,
        List<WeakTopicDto> WeakTopics,
        List<WeakTopicDto> StrongTopics,
        List<QuestionPerformanceDto> QuestionBreakdown
    );

    public record QuestionPerformanceDto(
        string QuestionText,
        string Topic,
        double CorrectRate,
        double AverageScore,
        int TotalAttempts
    );

    // ── At-risk alerts ────────────────────────────────────────────────────────

    public record TeachingAlertDto(
        string AlertId,
        string AlertType,   // "risk_escalation" | "attendance_drop" | "missing_assignment" | "performance_drop"
        string Severity,    // "info" | "warning" | "critical"
        string Title,
        string Message,
        string? StudentId,
        string? StudentName,
        string? OfferingId,
        bool IsRead,
        DateTime CreatedAt
    );

    // ── Excel export ──────────────────────────────────────────────────────────

    /// <summary>
    /// Row structure for the Excel export.
    /// The frontend receives this JSON array and builds the .xlsx file.
    /// Columns match exactly what teachers expect in a student report sheet.
    /// </summary>
    public record StudentExcelRowDto(
        // Columns 1-7: Identity
        string UniversityId,
        string StudentName,
        string BatchName,
        string GroupName,
        string DepartmentName,
        string CollegeName,
        string SubjectName,
        // Columns 8-13: Grades
        double? FinalScore,
        double? MidtermScore,
        double? CourseworkScore,
        double? FinalExamScore,
        string GradeCategory,   // "Pass" | "Fail" | "Pending"
        // Columns 14-16: Attendance
        int TotalSessions,
        int AttendedSessions,
        double AttendancePercent,
        // Columns 17-20: Assignments
        int TotalAssignments,
        int SubmittedAssignments,
        int MissingAssignments,
        double AssignmentCompletionRate,
        // Columns 21-23: Exams/Quizzes
        int TotalExams,
        double? AvgExamScore,
        double? AvgQuizScore,
        // Columns 24-26: Risk
        double RiskScore,
        string RiskLevel,
        string RiskFactors,
        // Columns 27-29: AI Companion
        int AiSessions,
        int StudyMinutes,
        int StreakDays
    );

    public record ExcelExportMetaDto(
        string ExportTitle,
        string GeneratedAt,
        string DoctorName,
        string SubjectName,
        string BatchName,
        string GroupName,
        int TotalRows,
        List<StudentExcelRowDto> Rows
    );

    // ── Filter/query params ───────────────────────────────────────────────────

    public record TeachingQueryFilter(
        string? OfferingId = null,
        string? BatchId = null,
        string? GroupId = null,
        string? DepartmentId = null,
        string? RiskLevel = null,       // filter by risk level
        string? Trend = null,           // "declining" | "improving"
        bool AtRiskOnly = false,
        int Page = 1,
        int PageSize = 50,
        string SortBy = "RiskScore",    // RiskScore | Name | Grade | Attendance
        string SortDir = "desc"
    );

    // ── AI Insights ───────────────────────────────────────────────────────────

    public record TeachingInsightDto(
        string InsightId,
        string Type,    // "weak_topic" | "class_declining" | "attendance_alert" | "risk_escalation"
        string Priority,
        string Title,
        string Insight,
        string? Action,
        string? OfferingId,
        DateTime GeneratedAt
    );
}
