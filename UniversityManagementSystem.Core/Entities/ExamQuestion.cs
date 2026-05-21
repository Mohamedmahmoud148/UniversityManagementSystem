using NUlid;

namespace UniversityManagementSystem.Core.Entities
{
    public enum QuestionType
    {
        MCQ = 0,
        TrueFalse = 1,
        Essay = 2,
        ShortAnswer = 3
    }

    public class ExamQuestion : BaseEntity
    {
        public string QuestionText { get; set; } = string.Empty;
        /// <summary>JSON array of choice strings, e.g. ["Paris","London","Cairo","Rome"]. Null for non-MCQ.</summary>
        public string? OptionsJson { get; set; }
        public string CorrectAnswer { get; set; } = string.Empty;
        public QuestionType QuestionType { get; set; } = QuestionType.MCQ;
        public int Mark { get; set; }

        public Ulid ExamId { get; set; }

        // Navigation Properties
        public Exam Exam { get; set; } = null!;
    }
}
