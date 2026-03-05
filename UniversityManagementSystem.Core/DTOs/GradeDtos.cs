using System;
using NUlid;

namespace UniversityManagementSystem.Core.DTOs
{
    public class GradeDto
    {
        public Ulid Id { get; set; }
        public Ulid StudentId { get; set; }
        public string StudentName { get; set; } = string.Empty;
        public Ulid SubjectOfferingId { get; set; }
        public string SubjectName { get; set; } = string.Empty;
        public double FinalScore { get; set; }
        public string GradeLetter { get; set; } = string.Empty;
        public double GradePoints { get; set; }
        public bool IsFinalized { get; set; }
        public DateTime CalculatedAt { get; set; }
    }

    public class UpdateGradeDto
    {
        public double FinalScore { get; set; }
        public string GradeLetter { get; set; } = string.Empty;
        public double GradePoints { get; set; }
        public bool IsFinalized { get; set; }
    }
}
