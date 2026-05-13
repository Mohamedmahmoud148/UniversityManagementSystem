using System;
using NUlid;
using UniversityManagementSystem.Core.Entities;

namespace UniversityManagementSystem.Core.DTOs
{
    // ── Create ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Sent by Admin to add one slot to a batch's weekly schedule.
    /// </summary>
    public class CreateScheduleEntryDto
    {
        /// <summary>The SubjectOffering ID — links to subject, doctor, batch, and semester.</summary>
        public string SubjectOfferingId { get; set; } = string.Empty;

        /// <summary>Optional: restrict to a specific group (section). Null = whole batch.</summary>
        public string? GroupId { get; set; }

        /// <summary>0 = Sunday, 1 = Monday, … 6 = Saturday.</summary>
        public DayOfWeek DayOfWeek { get; set; }

        /// <summary>Start time in "HH:mm" format (e.g. "09:00").</summary>
        public string StartTime { get; set; } = string.Empty;

        /// <summary>End time in "HH:mm" format (e.g. "10:30").</summary>
        public string EndTime { get; set; } = string.Empty;

        /// <summary>Lecture (0), Section (1), or Lab (2).</summary>
        public SessionType Type { get; set; } = SessionType.Lecture;

        /// <summary>Room / hall name, e.g. "Hall A", "Lab 3".</summary>
        public string Location { get; set; } = string.Empty;

        /// <summary>All (0), Odd weeks (1), Even weeks (2).</summary>
        public WeekParity WeekType { get; set; } = WeekParity.All;
    }

    // ── Update ────────────────────────────────────────────────────────────────

    /// <summary>All fields optional — only non-null values are applied.</summary>
    public class UpdateScheduleEntryDto
    {
        public DayOfWeek? DayOfWeek  { get; set; }
        public string?    StartTime  { get; set; }
        public string?    EndTime    { get; set; }
        public SessionType? Type     { get; set; }
        public string?    Location   { get; set; }
        public WeekParity?  WeekType { get; set; }
        public bool?      IsActive   { get; set; }
    }

    // ── Read ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Full schedule slot returned to frontend.
    /// Enriched with subject name, doctor name, batch/group names.
    /// </summary>
    public class ScheduleEntryDto
    {
        public string Id                { get; set; } = string.Empty;
        public string SubjectOfferingId { get; set; } = string.Empty;

        // Subject info
        public string SubjectCode       { get; set; } = string.Empty;
        public string SubjectName       { get; set; } = string.Empty;

        // Doctor / Teaching-assistant info
        public string DoctorName        { get; set; } = string.Empty;

        // Batch / Group
        public string BatchName         { get; set; } = string.Empty;
        public string? GroupName        { get; set; }

        // Slot
        public string DayOfWeek         { get; set; } = string.Empty;  // "Monday"
        public int    DayOfWeekNumber   { get; set; }                   // 0-6
        public string StartTime         { get; set; } = string.Empty;   // "09:00"
        public string EndTime           { get; set; } = string.Empty;   // "10:30"
        public string Type              { get; set; } = string.Empty;   // "Lecture"
        public string Location          { get; set; } = string.Empty;
        public string WeekType          { get; set; } = string.Empty;   // "All"
        public bool   IsActive          { get; set; }
    }

    // ── "Today's Schedule" response ───────────────────────────────────────────

    /// <summary>
    /// Lightweight projection used by the "today" endpoint.
    /// Sorted by StartTime ascending.
    /// </summary>
    public class TodayScheduleDto
    {
        public string Id          { get; set; } = string.Empty;
        public string SubjectName { get; set; } = string.Empty;
        public string SubjectCode { get; set; } = string.Empty;
        public string DoctorName  { get; set; } = string.Empty;
        public string StartTime   { get; set; } = string.Empty;
        public string EndTime     { get; set; } = string.Empty;
        public string Type        { get; set; } = string.Empty;
        public string Location    { get; set; } = string.Empty;
        public string? GroupName  { get; set; }
        /// <summary>True if this slot is happening right now (server clock).</summary>
        public bool   IsNow       { get; set; }
    }
}
