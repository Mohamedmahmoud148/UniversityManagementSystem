using System;
using System.Collections.Generic;
using NUlid;

namespace UniversityManagementSystem.Core.DTOs
{
    // ── Eligible Offerings ───────────────────────────────────────────────────

    public class EligibleOfferingDto
    {
        public string OfferingId      { get; set; } = string.Empty;
        public string SubjectName     { get; set; } = string.Empty;
        public string SubjectCode     { get; set; } = string.Empty;
        public int    CreditHours     { get; set; }
        public string DoctorName      { get; set; } = string.Empty;
        public string SemesterName    { get; set; } = string.Empty;
        public int    MaxCapacity     { get; set; }
        public int    EnrolledCount   { get; set; }
        public int    WaitlistCount   { get; set; }
        public bool   IsFull          { get; set; }

        public bool         IsEligible { get; set; }
        public List<string> Blockers   { get; set; } = new();
        public List<string> Warnings   { get; set; } = new();
    }

    // ── Enrollment Result ────────────────────────────────────────────────────

    public class EnrollmentResultDto
    {
        public bool         Success          { get; set; }
        public bool         AddedToWaitlist  { get; set; }
        public int?         WaitlistPosition { get; set; }
        public string       Message          { get; set; } = string.Empty;
        public List<string> Errors           { get; set; } = new();
        public List<string> Warnings         { get; set; } = new();
    }

    // ── Waitlist Result ──────────────────────────────────────────────────────

    public class WaitlistResultDto
    {
        public bool   Success  { get; set; }
        public int    Position { get; set; }
        public string Message  { get; set; } = string.Empty;
    }

    // ── Academic Status Summary (for dashboard) ──────────────────────────────

    public class AcademicStatusDto
    {
        public string StudentId       { get; set; } = string.Empty;
        public string StudentName     { get; set; } = string.Empty;
        public double GPA             { get; set; }
        public double CGPA            { get; set; }
        public double LastSemesterGPA { get; set; }
        public string Standing        { get; set; } = string.Empty;  // Good / Warning / Probation
        public string StandingColor   { get; set; } = string.Empty;  // green / yellow / red
        public int    EarnedHours     { get; set; }
        public int    RemainingHours  { get; set; }
        public int    TotalRequired   { get; set; }
        public int    CurrentLevel    { get; set; }
        public int    MaxAllowedHours { get; set; }  // computed from policy + GPA
        public int    WarningCount    { get; set; }
        public bool   HasWarning      { get; set; }
        public string? WarningMessage { get; set; }
    }

    // ── Import Dry-Run (preview) ─────────────────────────────────────────────

    public class ImportPreviewDto
    {
        public int              TotalRows    { get; set; }
        public int              WillSucceed  { get; set; }
        public int              WillSkip     { get; set; }
        public List<ImportRowErrorDto> Errors   { get; set; } = new();
        public List<ImportRowErrorDto> Warnings { get; set; } = new();
    }

    public class ImportRowErrorDto
    {
        public int    Row          { get; set; }
        public string Column       { get; set; } = string.Empty;
        public string Value        { get; set; } = string.Empty;
        public string Code         { get; set; } = string.Empty;
        public string Message      { get; set; } = string.Empty;
        public string Severity     { get; set; } = "Error";  // Error / Warning
        public string? SuggestedFix { get; set; }
    }
}
