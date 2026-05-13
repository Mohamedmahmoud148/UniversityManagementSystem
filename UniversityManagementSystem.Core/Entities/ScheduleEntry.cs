using System;
using NUlid;

namespace UniversityManagementSystem.Core.Entities
{
    /// <summary>
    /// Represents a single recurring weekly slot in a batch's timetable.
    ///
    /// One row = one lecture / section / lab that repeats every week
    /// on the same day at the same time.
    ///
    /// Admin creates these; Students and Doctors can query by batch/day.
    /// </summary>
    public class ScheduleEntry : BaseEntity
    {
        // ── Core FK ──────────────────────────────────────────────────────
        public Ulid SubjectOfferingId { get; set; }   // links to subject, doctor, batch, semester
        public Ulid BatchId           { get; set; }   // denormalised for fast query-by-batch
        public Ulid? GroupId          { get; set; }   // null = whole batch; set = specific group (section)

        // ── Schedule Slot ─────────────────────────────────────────────────
        public DayOfWeek DayOfWeek  { get; set; }   // 0=Sunday … 6=Saturday
        public TimeSpan  StartTime  { get; set; }   // e.g. 09:00:00
        public TimeSpan  EndTime    { get; set; }   // e.g. 10:30:00

        // ── Class Details ─────────────────────────────────────────────────
        public SessionType Type     { get; set; } = SessionType.Lecture;
        public string Location      { get; set; } = string.Empty;   // "Hall A", "Lab 3", etc.
        public WeekParity WeekType  { get; set; } = WeekParity.All; // All / Odd / Even weeks

        public bool IsActive { get; set; } = true;

        // ── Navigation ────────────────────────────────────────────────────
        public SubjectOffering SubjectOffering { get; set; } = null!;
        public Batch            Batch          { get; set; } = null!;
        public Group?           Group          { get; set; }
    }

    /// <summary>Lecture, Section (tutorial), or Lab.</summary>
    public enum SessionType
    {
        Lecture = 0,
        Section = 1,
        Lab     = 2
    }

    /// <summary>Which weeks the slot runs: every week, odd only, or even only.</summary>
    public enum WeekParity
    {
        All  = 0,
        Odd  = 1,
        Even = 2
    }
}
