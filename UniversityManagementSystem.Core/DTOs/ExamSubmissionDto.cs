using System.Collections.Generic;

namespace UniversityManagementSystem.Core.DTOs
{
    public class ExamSubmissionDto
    {
        public int ExamId { get; set; }
        public List<ExamAnswerDto> Answers { get; set; } = new();
    }

    public class ExamAnswerDto
    {
        public int QuestionId { get; set; }
        public string AnswerText { get; set; } = string.Empty;
    }
}
