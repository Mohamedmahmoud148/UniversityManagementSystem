using System;
using System.Collections.Generic;
using NUlid;

namespace UniversityManagementSystem.Core.DTOs
{
    public class ExamSubmissionResponseDto
    {
        public Ulid   Id          { get; set; }
        public Ulid   ExamId      { get; set; }
        public Ulid   StudentId   { get; set; }
        public string StudentName { get; set; } = string.Empty;
        public string StudentCode { get; set; } = string.Empty;
        public DateTime  SubmittedAt             { get; set; }
        public int?      DurationSpentMinutes    { get; set; }
        public double?   Score                   { get; set; }
        public double    TotalMarks              { get; set; }
        public double?   Percentage              { get; set; }
        public bool      IsGraded                { get; set; }
        public bool      IsCompleted             { get; set; }
        public bool      IsFlagged               { get; set; }
        public bool      NeedsManualGrading      { get; set; }
        public int       AutoGradedMcqCount      { get; set; }
        public int       PendingEssayCount       { get; set; }
    }

    // ── Detailed single-submission view (doctor opens "View Answers") ─────────
    public class SubmissionDetailDto
    {
        public Ulid     SubmissionId { get; set; }
        public string   StudentName  { get; set; } = string.Empty;
        public string   StudentCode  { get; set; } = string.Empty;
        public DateTime SubmittedAt  { get; set; }
        public double?  Score        { get; set; }
        public double   TotalMarks   { get; set; }
        public bool     IsGraded     { get; set; }
        public List<SubmissionAnswerDetailDto> Answers { get; set; } = new();
    }

    public class SubmissionAnswerDetailDto
    {
        public Ulid    QuestionId        { get; set; }
        public string  QuestionText      { get; set; } = string.Empty;
        public string  QuestionType      { get; set; } = string.Empty;  // Mcq | TrueFalse | Essay
        public int     Marks             { get; set; }
        public List<string>? Options     { get; set; }       // null for Essay
        public int?    CorrectIndex      { get; set; }       // null for Essay
        public int?    StudentAnswerIndex { get; set; }      // MCQ / TrueFalse choice index
        public string? StudentAnswerText { get; set; }       // Essay raw text
        public bool?   IsCorrect         { get; set; }       // null if essay not graded
        public double  AwardedScore      { get; set; }
        public string? ProfessorComment  { get; set; }
    }

    // ── Student's own submission result ──────────────────────────────────────
    public class StudentSubmissionResultDto
    {
        public Ulid     SubmissionId { get; set; }
        public double?  TotalScore   { get; set; }
        public double   TotalMarks   { get; set; }
        public double?  Percentage   { get; set; }
        public bool     IsGraded     { get; set; }
        public DateTime SubmittedAt  { get; set; }
        public List<StudentAnswerResultDto> Answers { get; set; } = new();
    }

    public class StudentAnswerResultDto
    {
        public Ulid    QuestionId  { get; set; }
        public string  AnswerText  { get; set; } = string.Empty;
        public bool?   IsCorrect   { get; set; }   // null for Essay until graded
        public double  EarnedMarks { get; set; }
    }

    // ── Grade a single essay question ─────────────────────────────────────────
    public class GradeQuestionDto
    {
        public Ulid   QuestionId { get; set; }
        public double Score      { get; set; }
        public string? Comment   { get; set; }
    }
}
