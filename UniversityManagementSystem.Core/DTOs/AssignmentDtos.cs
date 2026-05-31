using System;
using UniversityManagementSystem.Core.Entities;

namespace UniversityManagementSystem.Core.DTOs
{
    public record CreateAssignmentDto(
        string Title,
        string Description,
        string SubjectOfferingId,
        DateTime Deadline,
        double MaxGrade,
        bool AllowLateSubmission,
        bool AiGradingEnabled,
        string? GradingRubric);

    public record AssignmentDto(
        string Id,
        string Title,
        string Description,
        string SubjectName,
        DateTime Deadline,
        double MaxGrade,
        bool AiGradingEnabled,
        int SubmissionCount,
        DateTime CreatedAt);

    public record SubmitAssignmentDto(
        string AssignmentId,
        string? TextAnswer);

    public record GradeAssignmentSubmissionDto(
        string SubmissionId,
        double Grade,
        string? Feedback);

    public record AiGradingResultDto(
        double Score,
        string Feedback,
        string Strengths,
        string Weaknesses,
        double Confidence);

    public record SubmissionDto(
        string Id,
        string StudentName,
        DateTime SubmittedAt,
        bool IsLate,
        SubmissionStatus Status,
        double? Grade,
        string? Feedback,
        bool IsAiGraded,
        string? FileUrl,
        string? TextAnswer);
}
