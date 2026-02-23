using System;

namespace UniversityManagementSystem.Core.DTOs
{
    public class GradeDto
    {
        public int Id { get; set; }
        public int StudentId { get; set; }
        public string StudentName { get; set; } = string.Empty;
        public int SubjectOfferingId { get; set; }
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
