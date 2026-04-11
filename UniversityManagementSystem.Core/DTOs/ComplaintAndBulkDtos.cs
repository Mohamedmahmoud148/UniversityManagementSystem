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
        /// <summary>
        /// What the complaint targets.
        /// Allowed values: "Doctor" | "Exam" | "Grade" | "Other"
        /// </summary>
        [Required]
        [MaxLength(50)]
        public string TargetType { get; set; } = string.Empty;

        /// <summary>
        /// ULID string of the target entity (DoctorId / ExamId / GradeId).
        /// Optional for TargetType = "Other".
        /// </summary>
        [MaxLength(26)]
        public string? TargetId { get; set; }

        /// <summary>Subject offering the complaint applies to. Optional.</summary>
        public string? SubjectOfferingId { get; set; }

        /// <summary>Full complaint text (5–2000 characters).</summary>
        [Required]
        [MinLength(5)]
        [MaxLength(2000)]
        public string Message { get; set; } = string.Empty;
    }

    /// <summary>
    /// Single complaint record returned by GET /api/ai-tools/get-complaints.
    /// </summary>
    public class ComplaintDto
    {
        public string Id { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string TargetType { get; set; } = string.Empty;
        public string? TargetId { get; set; }
        public string? SubjectOfferingId { get; set; }
        public string? TargetDoctorId { get; set; }
        public string Message { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string? ResolutionNote { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    /// <summary>
    /// Query parameters for GET /api/ai-tools/get-complaints.
    /// All filters are optional and combinable.
    /// </summary>
    public class GetComplaintsQueryDto
    {
        /// <summary>Include complaints created on or after this date (UTC).</summary>
        public DateTime? From { get; set; }

        /// <summary>Include complaints created on or before this date (UTC).</summary>
        public DateTime? To { get; set; }

        /// <summary>Filter by subject offering ULID string.</summary>
        public string? SubjectOfferingId { get; set; }

        /// <summary>Filter by doctor ULID string (matches TargetDoctorId).</summary>
        public string? DoctorId { get; set; }

        /// <summary>Filter by complaint status (Pending/UnderReview/Resolved/Dismissed).</summary>
        public string? Status { get; set; }

        /// <summary>Page number (1-based). Default = 1.</summary>
        public int Page { get; set; } = 1;

        /// <summary>Results per page (max 100). Default = 20.</summary>
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
