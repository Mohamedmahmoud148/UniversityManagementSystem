using System.Collections.Generic;
using NUlid;

namespace UniversityManagementSystem.Core.DTOs
{
    public class ExamSubmissionDto
    {
        public Ulid ExamId { get; set; }
        public List<ExamAnswerDto> Answers { get; set; } = new();
    }

    public class ExamAnswerDto
    {
        public Ulid QuestionId { get; set; }
        public string AnswerText { get; set; } = string.Empty;
    }
}
