using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using NUlid;

namespace UniversityManagementSystem.Core.DTOs
{
    // ── Complaint DTOs ────────────────────────────────────────────────────────

    /// <summary>
    /// Request body for POST /api/ai-tools/create-complaint (Student only).
    /// </summary>
    public class CreateComplaintDto
    {
        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string TargetType { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? TargetId { get; set; }

        [Required]
        [MinLength(5)]
        [MaxLength(2000)]
        public string Message { get; set; } = string.Empty;
    }

    public class ComplaintStudentDto
    {
        public string Id { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string NationalId { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string AcademicCode { get; set; } = string.Empty;
    }

    public class ComplaintDto
    {
        public string Id { get; set; } = string.Empty;
        public string StudentId { get; set; } = string.Empty;
        public ComplaintStudentDto? Student { get; set; }
        public string Title { get; set; } = string.Empty;
        public string TargetType { get; set; } = string.Empty;
        public string TargetId { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Priority { get; set; } = string.Empty;
        public string? ResolutionNote { get; set; }
        public DateTime CreatedAt { get; set; }
        public ComplaintAnalysisDto? Analysis { get; set; }
    }

    public class ComplaintAnalysisDto
    {
        public double SentimentScore { get; set; }
        public string Category { get; set; } = string.Empty;
        public string Severity { get; set; } = string.Empty;
        public string AiSummary { get; set; } = string.Empty;
        public string SuggestedAction { get; set; } = string.Empty;
    }

    public class ComplaintClusterDto
    {
        public string Id { get; set; } = string.Empty;
        public string Topic { get; set; } = string.Empty;
        public string TargetType { get; set; } = string.Empty;
        public string TargetId { get; set; } = string.Empty;
        public int ComplaintCount { get; set; }
        public string AiSummary { get; set; } = string.Empty;
        public DateTime LastUpdated { get; set; }
    }

    /// <summary>
    /// Query parameters for GET /api/ai-tools/get-complaints.
    /// All filters are optional and combinable.
    /// </summary>
    public class GetComplaintsQueryDto
    {
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }
        public string? TargetType { get; set; }
        public string? TargetId { get; set; }
        public string? Status { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }

    /// <summary>Paginated complaints response.</summary>
    public class ComplaintsPageDto
    {
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public List<ComplaintDto> Items { get; set; } = new();
    }

    // ── Bulk Operations DTOs ──────────────────────────────────────────────────

    /// <summary>
    /// Standard result envelope for any bulk import operation.
    /// </summary>
    public class BulkOperationResultDto
    {
        public int TotalRows { get; set; }
        public int Inserted { get; set; }
        public int Skipped { get; set; }
        public int Failed { get; set; }
        public List<string> Errors { get; set; } = new();
        public bool Success => Failed == 0 && Errors.Count == 0;
    }

    /// <summary>
    /// Single row in the bulk grade upload file.
    /// Used internally for validation; file columns are parsed by BulkGradeImportService.
    /// </summary>
    public class BulkGradeRowDto
    {
        public string UniversityStudentId { get; set; } = string.Empty;
        public string SubjectOfferingId { get; set; } = string.Empty;
        public double FinalScore { get; set; }
        public string GradeLetter { get; set; } = string.Empty;
        public double GradePoints { get; set; }
    }
}
