using System;
using NUlid;

namespace UniversityManagementSystem.Core.Entities
{
    public class StudentGrade : BaseEntity
    {
        public double FinalScore { get; set; }
        public string GradeLetter { get; set; } = string.Empty;
        public double GradePoints { get; set; }
        public bool IsFinalized { get; set; }
        public DateTime CalculatedAt { get; set; } = DateTime.UtcNow;

        public Ulid StudentId { get; set; }
        public Ulid SubjectOfferingId { get; set; }

        // Navigation Properties
        public Student Student { get; set; } = null!;
        public SubjectOffering SubjectOffering { get; set; } = null!;
    }
}
