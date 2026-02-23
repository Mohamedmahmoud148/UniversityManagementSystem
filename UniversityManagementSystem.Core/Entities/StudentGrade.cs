using System;

namespace UniversityManagementSystem.Core.Entities
{
    public class StudentGrade : BaseEntity
    {
        public double FinalScore { get; set; }
        public string GradeLetter { get; set; } = string.Empty; // A, B+, C, etc.
        public double GradePoints { get; set; } // 4.0, 3.5, etc.
        public bool IsFinalized { get; set; }
        public DateTime CalculatedAt { get; set; } = DateTime.UtcNow;

        public int StudentId { get; set; }
        public int SubjectOfferingId { get; set; }

        // Navigation Properties
        public Student Student { get; set; } = null!;
        public SubjectOffering SubjectOffering { get; set; } = null!;
    }
}
