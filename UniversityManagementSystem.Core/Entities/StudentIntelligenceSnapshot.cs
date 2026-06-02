using System;
using NUlid;

namespace UniversityManagementSystem.Core.Entities
{
    /// <summary>
    /// Pre-computed analytics snapshot for a single student in a single subject offering.
    /// Recomputed every hour by TeachingIntelligenceBackgroundService.
    ///
    /// Why a snapshot table?
    ///   - The dashboard needs to display 100-300 students instantly.
    ///   - Computing attendance%, assignment completion, grades, and risk
    ///     for every student on every request is too expensive.
    ///   - This table is the "materialized view" — fast reads, async writes.
    ///
    /// The background job queries raw tables, computes all metrics, and
    /// upserts this row. The API reads ONLY this table for dashboard calls.
    /// </summary>
    public class StudentIntelligenceSnapshot : BaseEntity
    {
        public Ulid StudentId { get; set; }
        public Ulid SubjectOfferingId { get; set; }
        public Ulid DoctorId { get; set; }

        // ── Identity (denormalized for fast display) ──────────────────────
        public string StudentName { get; set; } = string.Empty;
        public string StudentUniversityId { get; set; } = string.Empty;
        public string BatchName { get; set; } = string.Empty;
        public string GroupName { get; set; } = string.Empty;
        public string DepartmentName { get; set; } = string.Empty;
        public string CollegeName { get; set; } = string.Empty;
        public string SubjectName { get; set; } = string.Empty;

        // ── Grade metrics ─────────────────────────────────────────────────
        /// Overall final score (0-100), null if not yet graded
        public double? FinalScore { get; set; }
        public double? MidtermScore { get; set; }
        public double? CourseworkScore { get; set; }
        public double? FinalExamScore { get; set; }
        /// Normalized grade score 0-100 used in risk calculation
        public double GradeScore { get; set; } = 0;

        // ── Assignment metrics ────────────────────────────────────────────
        public int TotalAssignments { get; set; } = 0;
        public int SubmittedAssignments { get; set; } = 0;
        public int LateSubmissions { get; set; } = 0;
        public int MissingAssignments { get; set; } = 0;
        /// Assignment completion rate 0-100
        public double AssignmentCompletionRate { get; set; } = 0;
        /// Average assignment grade 0-100 (among submitted)
        public double? AvgAssignmentGrade { get; set; }

        // ── Exam/Quiz metrics ─────────────────────────────────────────────
        public int TotalExams { get; set; } = 0;
        public int TakenExams { get; set; } = 0;
        /// Average exam score 0-100
        public double? AvgExamScore { get; set; }
        /// Average quiz score 0-100 (quizzes only)
        public double? AvgQuizScore { get; set; }

        // ── AI Companion engagement (from Redis/DB) ───────────────────────
        public int AiSessionCount { get; set; } = 0;
        public int AiStudyMinutes { get; set; } = 0;
        public int LearningStreakDays { get; set; } = 0;
        public double EngagementScore { get; set; } = 0;

        // ── Risk Engine output ────────────────────────────────────────────
        /// Risk score 0-100: higher = more at risk
        public double RiskScore { get; set; } = 0;
        public RiskLevel RiskLevel { get; set; } = RiskLevel.Low;
        /// JSON array of risk factor strings
        public string RiskFactors { get; set; } = "[]";
        /// AI-generated risk explanation (1-2 sentences)
        public string RiskExplanation { get; set; } = string.Empty;
        /// Recommended action for the doctor
        public string RecommendedAction { get; set; } = string.Empty;

        // ── Trend indicators ──────────────────────────────────────────────
        /// Change in grade since last snapshot
        public double GradeTrend { get; set; } = 0;
        /// "improving" | "declining" | "stable"
        public string OverallTrend { get; set; } = "stable";

        public DateTime ComputedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public Student Student { get; set; } = null!;
        public SubjectOffering SubjectOffering { get; set; } = null!;
        public Doctor Doctor { get; set; } = null!;
    }
}
