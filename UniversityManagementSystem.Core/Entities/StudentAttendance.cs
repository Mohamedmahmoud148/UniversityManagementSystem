using System;
using NUlid;

namespace UniversityManagementSystem.Core.Entities
{
    public class StudentAttendance : BaseEntity
    {
        public Ulid AttendanceSessionId { get; set; }
        public Ulid StudentId { get; set; }
        public bool IsPresent { get; set; }
        public DateTime? CheckInTime { get; set; }
        public string? Remarks { get; set; }

        // Navigation Properties
        public AttendanceSession AttendanceSession { get; set; } = null!;
        public Student Student { get; set; } = null!;
    }
}
