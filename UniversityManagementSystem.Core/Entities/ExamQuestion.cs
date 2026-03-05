using NUlid;

namespace UniversityManagementSystem.Core.Entities
{
    public class ExamQuestion : BaseEntity
    {
        public string QuestionText { get; set; } = string.Empty;
        public string CorrectAnswer { get; set; } = string.Empty;
        public int Mark { get; set; }

        public Ulid ExamId { get; set; }

        // Navigation Properties
        public Exam Exam { get; set; } = null!;
    }
}
