using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using UniversityManagementSystem.Core.Entities;
using NUlid;

namespace UniversityManagementSystem.Core.DTOs
{
    public class ExamDto
    {
        public Ulid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public int TotalMarks { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public string Mode { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string? FilePath { get; set; }
        public Ulid CreatedByDoctorId { get; set; }
        public Ulid SubjectOfferingId { get; set; }
        public string SubjectName { get; set; } = string.Empty;
        public bool IsRandomized { get; set; }
        public int QuestionsPerStudent { get; set; }
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

        public ExamStatus Status { get; set; } = ExamStatus.Draft;

        public List<CreateExamQuestionDto> Questions { get; set; } = new();
    }

    public class ExamQuestionDto
    {
        public Ulid Id { get; set; }
        public string QuestionText { get; set; } = string.Empty;
        /// <summary>MCQ choices. Null for TrueFalse / Essay. Hidden from student after submission.</summary>
        public List<string>? Options { get; set; }
        /// <summary>"MCQ" | "TrueFalse" | "Essay"</summary>
        public string QuestionType { get; set; } = "MCQ";
        public int Mark { get; set; }
        /// <summary>Only populated for Doctor / Admin roles — never sent to Student.</summary>
        public string? CorrectAnswer { get; set; }
    }

    public class CreateExamQuestionDto
    {
        [Required]
        public string QuestionText { get; set; } = string.Empty;

        /// <summary>MCQ answer choices. Required when QuestionType == MCQ.</summary>
        public List<string>? Options { get; set; }

        [Required]
        public string CorrectAnswer { get; set; } = string.Empty;

        public int QuestionType { get; set; } = 0; // 0=MCQ, 1=TrueFalse, 2=Essay

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

        public ExamStatus Status { get; set; } = ExamStatus.Draft;
    }

    public class CreateAiExamRequest
    {
        [Required]
        public Ulid SubjectOfferingId { get; set; }

        public string Difficulty { get; set; } = "Medium";
        public int QuestionCount { get; set; } = 10;
        public string ExamType { get; set; } = "Final";
        public List<string> Topics { get; set; } = new();

        /// <summary>
        /// When true, a large question pool is generated and each student receives
        /// a unique random subset of QuestionsPerStudent questions.
        /// </summary>
        public bool IsRandomized { get; set; } = false;

        /// <summary>
        /// How many questions each student sees. Only used when IsRandomized=true.
        /// Must be less than QuestionCount (which becomes the pool size).
        /// Defaults to half the pool if not specified.
        /// </summary>
        public int QuestionsPerStudent { get; set; } = 0;
    }
}
