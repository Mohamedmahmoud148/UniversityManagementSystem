using System;
using NUlid;

namespace UniversityManagementSystem.Core.Entities
{
    /// <summary>
    /// Persists GPA, academic standing, and credit hour progress per student.
    /// Updated automatically after every grade finalization — never recalculated on-the-fly.
    /// One row per student (1-to-1 with Student).
    /// </summary>
    public class StudentAcademicStatus : BaseEntity
    {
        public Ulid StudentId { get; set; }

        // ── GPA ────────────────────────────────────────────────────────
        public double GPA               { get; set; }
        public double CGPA              { get; set; }
        public double LastSemesterGPA   { get; set; }
        public DateTime? LastCalculatedAt { get; set; }

        // ── Credit Hours ───────────────────────────────────────────────
        public int EarnedCreditHours    { get; set; }
        public int RemainingCreditHours { get; set; }
        public int TotalRequiredHours   { get; set; }

        // ── Standing ───────────────────────────────────────────────────
        /// <summary>Good / Warning / Probation / Suspended / Graduated / Expelled</summary>
        public AcademicStanding Standing { get; set; } = AcademicStanding.Good;
        public int WarningCount          { get; set; }
        public int CurrentLevel          { get; set; } = 1;

        // Navigation
        public Student Student { get; set; } = null!;
    }

    public enum AcademicStanding
    {
        Good       = 0,
        Warning    = 1,
        Probation  = 2,
        Suspended  = 3,
        Graduated  = 4,
        Expelled   = 5
    }
}
