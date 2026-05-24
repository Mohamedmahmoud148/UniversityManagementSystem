using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using UniversityManagementSystem.Core.Entities;
using NUlid;

namespace UniversityManagementSystem.Core.DTOs
{
    public class ExamDto
    {
        public Ulid Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string? Instructions { get; set; }
        public string Type { get; set; } = string.Empty;
        public int TotalMarks { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public int DurationMinutes { get; set; }
        public string Mode { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string? FilePath { get; set; }
        public Ulid CreatedByDoctorId { get; set; }
        public Ulid SubjectOfferingId { get; set; }
        public string SubjectName { get; set; } = string.Empty;
        public bool IsRandomized { get; set; }
        public int QuestionsPerStudent { get; set; }
        public bool AllowLateSubmission { get; set; }
        public int LateSubmissionWindowMinutes { get; set; }
        public int QuestionCount { get; set; }
        public List<ExamQuestionDto> Questions { get; set; } = new();

        /// <summary>Populated only in student-facing endpoints. Null for doctor/admin views.</summary>
        public bool? HasSubmitted { get; set; }
    }

    public class CreateExamDto
    {
        /// <summary>Can be sent in body OR as ?subjectOfferingId= query param — body takes priority.</summary>
        public string? SubjectOfferingId { get; set; }

        [Required]
        public string Title { get; set; } = string.Empty;

        public string? Instructions { get; set; }

        [Required]
        public ExamType Type { get; set; }

        [Required]
        public DateTime StartTime { get; set; }

        [Required]
        public DateTime EndTime { get; set; }

        /// <summary>Override duration in minutes. If 0, calculated from EndTime - StartTime.</summary>
        public int DurationMinutes { get; set; } = 0;

        public ExamStatus Status { get; set; } = ExamStatus.Draft;

        public bool AllowLateSubmission { get; set; } = false;
        public int LateSubmissionWindowMinutes { get; set; } = 0;

        public bool IsRandomized { get; set; } = false;
        public int QuestionsPerStudent { get; set; } = 0;

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

        /// <summary>
        /// Pre-generated questions from FastAPI. When provided, the backend skips
        /// the second AI call and persists these questions directly — avoids double billing.
        /// </summary>
        public List<CreateExamQuestionDto>? PreGeneratedQuestions { get; set; }
    }

    public class UploadPdfExamRequest
    {
        public string? SubjectOfferingId { get; set; }

        [Required]
        public IFormFile File { get; set; } = null!;
    }

    /// <summary>Request to preview AI-generated questions without creating an exam.</summary>
    public class PreviewAiQuestionsDto
    {
        public string Difficulty { get; set; } = "Medium";
        public int QuestionCount { get; set; } = 10;
        public string ExamType { get; set; } = "Final";
        public List<string> Topics { get; set; } = new();
    }

    public class PreviewQuestionsFromPdfRequest
    {
        [Required]
        public IFormFile File { get; set; } = null!;
        public int QuestionCount { get; set; } = 10;
        public string Difficulty { get; set; } = "Medium";
        public string ExamType { get; set; } = "Final";
    }

    /// <summary>Auto-save draft answers during exam session (no final submit).</summary>
    public class SaveProgressDto
    {
        [Required]
        public List<ExamAnswerDto> Answers { get; set; } = new();
    }

    /// <summary>Response after saving progress.</summary>
    public class SaveProgressResponseDto
    {
        public DateTime SavedAt { get; set; }
        public int AnswersSaved { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    /// <summary>Student's exam session state — returned when student opens exam.</summary>
    public class ExamSessionDto
    {
        public Ulid ExamId { get; set; }
        public string ExamCode { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string? Instructions { get; set; }
        public int DurationMinutes { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public bool IsSubmitted { get; set; }
        public DateTime? SubmittedAt { get; set; }
        public List<ExamAnswerDto> DraftAnswers { get; set; } = new();
        public List<ExamQuestionDto> Questions { get; set; } = new();
        public int SecondsRemaining { get; set; }
    }

    /// <summary>Exam analytics for doctor.</summary>
    public class ExamAnalyticsDto
    {
        public Ulid ExamId { get; set; }
        public string ExamTitle { get; set; } = string.Empty;
        public int TotalEnrolled { get; set; }
        public int TotalSubmitted { get; set; }
        public int TotalGraded { get; set; }
        public int TotalPassed { get; set; }
        public int TotalFailed { get; set; }
        public double AverageScore { get; set; }
        public double HighestScore { get; set; }
        public double LowestScore { get; set; }
        public double PassRate { get; set; }
        public List<QuestionAnalyticsDto> QuestionStats { get; set; } = new();
    }

    public class QuestionAnalyticsDto
    {
        public Ulid QuestionId { get; set; }
        public string QuestionText { get; set; } = string.Empty;
        public double CorrectRate { get; set; }
        public int TotalAnswered { get; set; }
        public int TotalCorrect { get; set; }
    }
}
