using System;

namespace UniversityManagementSystem.Core.Entities
{
    public class StudentAttendance : BaseEntity
    {
        public int AttendanceSessionId { get; set; }
        public int StudentId { get; set; }
        public bool IsPresent { get; set; }
        public DateTime? CheckInTime { get; set; }
        public string? Remarks { get; set; }

        // Navigation Properties
        public AttendanceSession AttendanceSession { get; set; } = null!;
        public Student Student { get; set; } = null!;
    }
}
