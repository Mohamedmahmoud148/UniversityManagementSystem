using System;
using System.Collections.Generic;
using NUlid;

namespace UniversityManagementSystem.Core.DTOs
{
    // ── Lightweight summary DTOs ──────────────────────────────────────────────

    public record DoctorSummaryDto
    {
        public string Id           { get; init; } = string.Empty;
        public string Code         { get; init; } = string.Empty;
        public string FullName     { get; init; } = string.Empty;
        public string Email        { get; init; } = string.Empty;
        public string DepartmentId { get; init; } = string.Empty;
        public string DepartmentName { get; init; } = string.Empty;
        public string CollegeName  { get; init; } = string.Empty;
    }

    public record StudentSummaryDto
    {
        public string Id                 { get; init; } = string.Empty;
        public string Code               { get; init; } = string.Empty;
        public string FullName           { get; init; } = string.Empty;
        public string UniversityStudentId { get; init; } = string.Empty;
        public string Email              { get; init; } = string.Empty;
        public string BatchName          { get; init; } = string.Empty;
        public string DepartmentName     { get; init; } = string.Empty;
        public string CollegeName        { get; init; } = string.Empty;
    }

    public record OfferingSummaryDto
    {
        public string Id             { get; init; } = string.Empty;
        public string Code           { get; init; } = string.Empty;
        public string SubjectName    { get; init; } = string.Empty;
        public string SubjectCode    { get; init; } = string.Empty;
        public string DoctorName     { get; init; } = string.Empty;
        public string DoctorId       { get; init; } = string.Empty;
        public string DepartmentName { get; init; } = string.Empty;
        public string BatchName      { get; init; } = string.Empty;
        public string SemesterName   { get; init; } = string.Empty;
        public int    MaxCapacity    { get; init; }
        public int    EnrolledCount  { get; init; }
    }

    // ── Analytics aggregation DTOs ────────────────────────────────────────────

    public record DepartmentCountDto
    {
        public string DepartmentId   { get; init; } = string.Empty;
        public string DepartmentName { get; init; } = string.Empty;
        public string CollegeName    { get; init; } = string.Empty;
        public int    StudentCount   { get; init; }
        public int    DoctorCount    { get; init; }
    }

    public record BatchCountDto
    {
        public string BatchId        { get; init; } = string.Empty;
        public string BatchName      { get; init; } = string.Empty;
        public string BatchCode      { get; init; } = string.Empty;
        public string DepartmentName { get; init; } = string.Empty;
        public string CollegeName    { get; init; } = string.Empty;
        public int    StudentCount   { get; init; }
    }

    public record DoctorWorkloadDto
    {
        public string DoctorId        { get; init; } = string.Empty;
        public string DoctorCode      { get; init; } = string.Empty;
        public string FullName        { get; init; } = string.Empty;
        public string DepartmentName  { get; init; } = string.Empty;
        public int    OfferingCount   { get; init; }
        public int    TotalStudents   { get; init; }
    }

    public record SubjectEnrollmentStatsDto
    {
        public string SubjectId      { get; init; } = string.Empty;
        public string SubjectCode    { get; init; } = string.Empty;
        public string SubjectName    { get; init; } = string.Empty;
        public int    OfferingCount  { get; init; }
        public int    EnrolledCount  { get; init; }
    }

    public record OfferingEnrollmentStatsDto
    {
        public string OfferingId     { get; init; } = string.Empty;
        public string OfferingCode   { get; init; } = string.Empty;
        public string SubjectName    { get; init; } = string.Empty;
        public string DoctorName     { get; init; } = string.Empty;
        public string DepartmentName { get; init; } = string.Empty;
        public string SemesterName   { get; init; } = string.Empty;
        public int    EnrolledCount  { get; init; }
        public int    MaxCapacity    { get; init; }
        public double FillRate       { get; init; } // percentage 0-100
        public double? AverageGrade  { get; init; }
    }

    public record AnalyticsSummaryDto
    {
        public int TotalStudents    { get; init; }
        public int TotalDoctors     { get; init; }
        public int TotalOfferings   { get; init; }
        public int TotalEnrollments { get; init; }
        public int TotalColleges    { get; init; }
        public int TotalDepartments { get; init; }
        public int TotalBatches     { get; init; }
        public IReadOnlyList<DepartmentCountDto>      TopDepartments { get; init; } = [];
        public IReadOnlyList<SubjectEnrollmentStatsDto> TopSubjects  { get; init; } = [];
    }
}
