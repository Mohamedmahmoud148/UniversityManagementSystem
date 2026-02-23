using System;

namespace UniversityManagementSystem.Core.Entities
{
    public class ExamSubmission : BaseEntity
    {
        public double? Score { get; set; }
        public bool IsGraded { get; set; }
        public string AnswersJson { get; set; } = "[]"; // Stores student answers
        public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;

        public int ExamId { get; set; }
        public int StudentId { get; set; }

        // Navigation Properties
        public Exam Exam { get; set; } = null!;
        public Student Student { get; set; } = null!;
    }
}
