using System;
using NUlid;

namespace UniversityManagementSystem.Core.Entities
{
    public enum RiskLevel { Low = 0, Medium = 1, High = 2, Critical = 3 }

    public class AcademicRiskScore : BaseEntity
    {
        public Ulid StudentId { get; set; }
        public Ulid SubjectOfferingId { get; set; }
        public double AttendancePercent { get; set; }
        public double AverageGrade { get; set; }
        public RiskLevel RiskLevel { get; set; }
        public string AiRecommendation { get; set; } = string.Empty;
        public DateTime AnalyzedAt { get; set; }

        // Navigation Properties
        public Student Student { get; set; } = null!;
        public SubjectOffering SubjectOffering { get; set; } = null!;
    }
}
