using System;

namespace UniversityManagementSystem.Core.DTOs
{
    public class ExamSubmissionResponseDto
    {
        public int Id { get; set; }
        public int ExamId { get; set; }
        public int StudentId { get; set; }
        public string StudentName { get; set; } = string.Empty;
        public DateTime SubmittedAt { get; set; }
        public double? Score { get; set; }
        public bool IsGraded { get; set; }
        public string AnswersJson { get; set; } = string.Empty; // Optional: include answers if needed by doctor
    }
}
