using System;
using NUlid;

namespace UniversityManagementSystem.Core.DTOs
{
    public class ExamSubmissionResponseDto
    {
        public Ulid Id { get; set; }
        public Ulid ExamId { get; set; }
        public Ulid StudentId { get; set; }
        public string StudentName { get; set; } = string.Empty;
        public DateTime SubmittedAt { get; set; }
        public double? Score { get; set; }
        public bool IsGraded { get; set; }
        public bool IsCompleted { get; set; }
        public bool NeedsManualGrading { get; set; }
        public string AnswersJson { get; set; } = string.Empty;
    }
}
