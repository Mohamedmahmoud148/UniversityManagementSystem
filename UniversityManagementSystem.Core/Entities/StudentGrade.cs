using System;
using NUlid;

namespace UniversityManagementSystem.Core.Entities
{
    public class StudentGrade : BaseEntity
    {
        public double FinalScore { get; set; }
        public string GradeLetter { get; set; } = string.Empty;
        public double GradePoints { get; set; }
        
        // Detailed offline and platform scores
        public double? MidtermScore { get; set; }
        public double? CourseworkScore { get; set; }
        public double? FinalExamScore { get; set; }
        public double? PlatformScore { get; set; }

        public bool IsFinalized { get; set; }
        public DateTime CalculatedAt { get; set; } = DateTime.UtcNow;

        public Ulid StudentId { get; set; }
        public Ulid SubjectOfferingId { get; set; }

        // Navigation Properties
        public Student Student { get; set; } = null!;
        public SubjectOffering SubjectOffering { get; set; } = null!;
    }
}
