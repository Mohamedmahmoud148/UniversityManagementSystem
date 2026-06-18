using System;
using NUlid;

namespace UniversityManagementSystem.Core.Entities
{
    public class LectureQuiz : BaseEntity
    {
        public Ulid RecordingId { get; set; }
        public string Question { get; set; } = string.Empty;
        public string OptionA { get; set; } = string.Empty;
        public string OptionB { get; set; } = string.Empty;
        public string OptionC { get; set; } = string.Empty;
        public string OptionD { get; set; } = string.Empty;
        public string CorrectAnswer { get; set; } = string.Empty;
        public string Explanation { get; set; } = string.Empty;

        // Navigation
        public LectureRecording Recording { get; set; } = null!;
    }
}
