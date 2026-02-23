using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using UniversityManagementSystem.Core.Entities;

namespace UniversityManagementSystem.Core.DTOs
{
    public class ExamDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty; // Enum as string
        public int TotalMarks { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public bool IsPublished { get; set; }
        
        public int SubjectOfferingId { get; set; }
        public string SubjectName { get; set; } = string.Empty;

        public List<ExamQuestionDto> Questions { get; set; } = new();
    }

    public class CreateExamDto
    {
        [Required]
        public string Title { get; set; } = string.Empty;

        [Required]
        public ExamType Type { get; set; }

        [Required]
        public DateTime StartTime { get; set; }

        [Required]
        public DateTime EndTime { get; set; }

        public bool IsPublished { get; set; }

        public List<CreateExamQuestionDto> Questions { get; set; } = new();
    }

    public class ExamQuestionDto
    {
        public int Id { get; set; }
        public string QuestionText { get; set; } = string.Empty;
        public int Mark { get; set; }
        // CorrectAnswer kept hidden in basic DTO for security if needed, 
        // but for now let's expose it to Doctor or specific endpoints. 
        // Logic will handle hiding it for students.
    }

    public class CreateExamQuestionDto
    {
        [Required]
        public string QuestionText { get; set; } = string.Empty;

        [Required]
        public string CorrectAnswer { get; set; } = string.Empty;

        [Range(1, 100)]
        public int Mark { get; set; }
    }

    public class UpdateExamDto
    {
        [Required]
        public string Title { get; set; } = string.Empty;

        [Required]
        public ExamType Type { get; set; }

        [Required]
        public DateTime StartTime { get; set; }

        [Required]
        public DateTime EndTime { get; set; }

        public bool IsPublished { get; set; }
    }
}
