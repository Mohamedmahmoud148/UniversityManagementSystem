using System;

namespace UniversityManagementSystem.Core.Entities
{
    public class ExamQuestion : BaseEntity
    {
        public string QuestionText { get; set; } = string.Empty;
        public string CorrectAnswer { get; set; } = string.Empty; // Simple text match for now
        public int Mark { get; set; }

        public int ExamId { get; set; }

        // Navigation Properties
        public Exam Exam { get; set; } = null!;
    }
}
