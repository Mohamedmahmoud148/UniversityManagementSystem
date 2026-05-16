using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;
using UniversityManagementSystem.Core.Entities;
using NUlid;

namespace UniversityManagementSystem.Core.DTOs
{
    public class RegulationSubjectDto
    {
        public Ulid SubjectId { get; set; }
        public int Semester { get; set; }
        public bool IsRequired { get; set; }
    }
    public class RegulationDto
    {
        public Ulid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Content { get; set; }
        public string Type { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        /// <summary>The ID of the attached file, if any.</summary>
        public string? FileId { get; set; }
        /// <summary>60-minute signed download URL. Null if no file is attached.</summary>
        public string? FileUrl { get; set; }

        public string? DepartmentId { get; set; }
        public List<RegulationSubjectDto> Subjects { get; set; } = new();
    }

    /// <summary>
    /// Used with multipart/form-data.
    /// Send EITHER Content (text) OR File (PDF/Word/Excel/TXT). Both are optional,
    /// but at least one must be present.
    /// </summary>
    public class CreateRegulationDto
    {
        public string Title { get; set; } = string.Empty;

        /// <summary>Text content of the regulation (optional when File is provided).</summary>
        public string? Content { get; set; }

        /// <summary>Regulation type enum value (int).</summary>
        public RegulationType Type { get; set; }

        /// <summary>
        /// File attachment (PDF, Word, Excel, TXT).
        /// Bound from multipart/form-data — ignored in JSON serialization.
        /// </summary>
        [JsonIgnore]
        public IFormFile? File { get; set; }

        public string? DepartmentId { get; set; }

        /// <summary>
        /// JSON string representing a list of RegulationSubjectDto.
        /// Required because this is a multipart/form-data request.
        /// </summary>
        public string? SubjectsJson { get; set; }
    }

    /// <summary>
    /// Used with multipart/form-data for PUT /api/Regulations/{id}.
    /// </summary>
    public class UpdateRegulationDto
    {
        public string Title { get; set; } = string.Empty;
        public string? Content { get; set; }
        public RegulationType Type { get; set; }
        public bool IsActive { get; set; }

        [JsonIgnore]
        public IFormFile? File { get; set; }

        public string? DepartmentId { get; set; }
        public string? SubjectsJson { get; set; }
    }

    // ── Academic Roadmap DTOs ─────────────────────────────────────────────────

    /// <summary>Status of a single subject in the student's academic roadmap.</summary>
    public record SubjectStatusDto
    {
        public string SubjectId     { get; init; } = string.Empty;
        public string SubjectName   { get; init; } = string.Empty;
        public string SubjectCode   { get; init; } = string.Empty;
        public int    CreditHours   { get; init; }
        public bool   IsRequired    { get; init; }

        /// <summary>passed | failed | enrolled | upcoming</summary>
        public string Status        { get; init; } = "upcoming";

        public string? GradeLetter  { get; init; }
        public double? GradePoints  { get; init; }
        public double? FinalScore   { get; init; }
    }

    /// <summary>One semester's worth of subjects with aggregate stats.</summary>
    public record SemesterRoadmapDto
    {
        public int    SemesterNumber    { get; init; }

        /// <summary>completed | in_progress | upcoming</summary>
        public string Status            { get; init; } = "upcoming";

        public int    TotalSubjects     { get; init; }
        public int    PassedSubjects    { get; init; }
        public int    FailedSubjects    { get; init; }
        public int    EnrolledSubjects  { get; init; }
        public int    TotalCreditHours  { get; init; }
        public int    EarnedCreditHours { get; init; }

        public List<SubjectStatusDto> Subjects { get; init; } = [];
    }

    /// <summary>
    /// Full personalized academic roadmap for a student.
    /// Returned by GET /api/Regulations/my-roadmap.
    /// Enables the AI to answer ANY regulation or progress question with one call.
    /// </summary>
    public record AcademicRoadmapDto
    {
        public string RegulationId      { get; init; } = string.Empty;
        public string RegulationTitle   { get; init; } = string.Empty;
        public string DepartmentName    { get; init; } = string.Empty;
        public string CollegeName       { get; init; } = string.Empty;
        public string BatchName         { get; init; } = string.Empty;

        public int    TotalSemesters         { get; init; }
        public int    TotalCreditHours       { get; init; }
        public int    CompletedCreditHours   { get; init; }
        public int    RemainingCreditHours   { get; init; }
        public int    TotalSubjects          { get; init; }
        public int    PassedSubjects         { get; init; }
        public int    FailedSubjects         { get; init; }
        public int    CurrentlyEnrolled      { get; init; }
        public double? CurrentGpa            { get; init; }

        public List<SemesterRoadmapDto>  Semesters          { get; init; } = [];

        /// <summary>Subjects in the next semester not yet enrolled in.</summary>
        public List<SubjectStatusDto>    RecommendedNext    { get; init; } = [];

        /// <summary>Required subjects the student failed and must retake.</summary>
        public List<SubjectStatusDto>    MustRetake         { get; init; } = [];
    }
}
