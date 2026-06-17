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

    /// <summary>A single activity (assignment / exam) attached to a course.</summary>
    public record RoadmapActivityDto
    {
        public string   Id            { get; init; } = string.Empty;
        /// <summary>assignment | quiz | midterm | final</summary>
        public string   Type          { get; init; } = string.Empty;
        public string   Title         { get; init; } = string.Empty;
        public DateTime DueDate       { get; init; }
        public DateTime? SubmittedAt  { get; init; }
        /// <summary>pending | submitted | graded | overdue | missed</summary>
        public string   Status        { get; init; } = "pending";
        public double?  Score         { get; init; }
        public double   MaxScore      { get; init; }
    }

    /// <summary>Status of a single subject in the student's academic roadmap.</summary>
    public record SubjectStatusDto
    {
        public string SubjectId     { get; init; } = string.Empty;
        public string SubjectName   { get; init; } = string.Empty;
        public string SubjectCode   { get; init; } = string.Empty;
        public int    CreditHours   { get; init; }
        public bool   IsRequired    { get; init; }

        /// <summary>passed | failed | in_progress | withdrawn | upcoming</summary>
        public string Status        { get; init; } = "upcoming";

        public string? GradeLetter  { get; init; }
        public double? GradePoints  { get; init; }
        public double? FinalScore   { get; init; }

        // ── New journey fields ──
        public bool   IsRetake      { get; init; }
        public int    RetakeCount   { get; init; }
        public List<RoadmapActivityDto> Activities { get; init; } = [];
    }

    /// <summary>One real semester in the student's journey with aggregate stats.</summary>
    public record SemesterRoadmapDto
    {
        // ── Identity (new: real semester data) ──
        public string   SemesterId        { get; init; } = string.Empty;
        public string   SemesterName      { get; init; } = string.Empty;
        public string   AcademicYearName  { get; init; } = string.Empty;
        public DateTime StartDate         { get; init; }
        public DateTime EndDate           { get; init; }

        /// <summary>Chronological position in student's journey (1 = first semester).</summary>
        public int    SemesterNumber    { get; init; }

        /// <summary>completed | in_progress | upcoming</summary>
        public string Status            { get; init; } = "upcoming";

        // ── GPA (new) ──
        public double? SemesterGpa         { get; init; }
        public double? CumulativeGpaAfter  { get; init; }

        // ── Counters ──
        public int    TotalSubjects     { get; init; }
        public int    PassedSubjects    { get; init; }
        public int    FailedSubjects    { get; init; }
        /// <summary>Courses currently in progress (kept as EnrolledSubjects for AI compat).</summary>
        public int    EnrolledSubjects  { get; init; }
        public int    WithdrawnSubjects { get; init; }
        public int    TotalCreditHours  { get; init; }
        public int    EarnedCreditHours { get; init; }

        public List<SubjectStatusDto> Subjects { get; init; } = [];
    }

    /// <summary>
    /// Full personalized academic roadmap — student-journey-centric.
    /// Returned by GET /api/Regulations/my-roadmap.
    /// Semesters are built from REAL enrollments, not the static regulation.
    /// </summary>
    public record AcademicRoadmapDto
    {
        // ── Student info (new) ──
        public string StudentId     { get; init; } = string.Empty;
        public string StudentName   { get; init; } = string.Empty;
        public string StudentCode   { get; init; } = string.Empty;

        // ── Regulation context (optional) ──
        public string? RegulationId      { get; init; }
        public string? RegulationTitle   { get; init; }
        public string DepartmentName    { get; init; } = string.Empty;
        public string CollegeName       { get; init; } = string.Empty;
        public string BatchName         { get; init; } = string.Empty;

        // ── Overall progress ──
        public int    TotalSemesters         { get; init; }
        public int    TotalCreditHours       { get; init; }
        public int    CompletedCreditHours   { get; init; }
        public int    RemainingCreditHours   { get; init; }
        public int    TotalSubjects          { get; init; }
        public int    PassedSubjects         { get; init; }
        public int    FailedSubjects         { get; init; }
        public int    CurrentlyEnrolled      { get; init; }
        public double? CurrentGpa            { get; init; }
        public double GraduationProgressPercent { get; init; }

        // ── Journey (real semesters ordered by date) ──
        public List<SemesterRoadmapDto>  Semesters          { get; init; } = [];

        // ── What's next ──
        /// <summary>Subjects from regulation not yet enrolled in.</summary>
        public List<SubjectStatusDto>    RecommendedNext    { get; init; } = [];
        /// <summary>Required subjects the student failed and must retake.</summary>
        public List<SubjectStatusDto>    MustRetake         { get; init; } = [];

        // ── AI recommendations (new) ──
        public List<string> Recommendations  { get; init; } = [];
        public List<string> AcademicWarnings { get; init; } = [];
    }
}
