using System;
using NUlid;

namespace UniversityManagementSystem.Core.Entities
{
    public class ExamSubmission : BaseEntity
    {
        public double? Score { get; set; }
        public bool IsGraded { get; set; }
        public string AnswersJson { get; set; } = "[]";
        public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;

        public Ulid ExamId { get; set; }
        public Ulid StudentId { get; set; }

        // Navigation Properties
        public Exam Exam { get; set; } = null!;
        public Student Student { get; set; } = null!;
    }
}
