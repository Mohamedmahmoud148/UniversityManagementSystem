using System;
using NUlid;

namespace UniversityManagementSystem.Core.Entities
{
    public class AttendanceSession : BaseEntity
    {
        public Ulid SubjectId { get; set; }
        public DateTime SessionDate { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public string QrCodeContent { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;

        public Ulid? DoctorId { get; set; }
        public Ulid? TeachingAssistantId { get; set; }

        // Navigation Properties
        public Doctor? Doctor { get; set; }
        public TeachingAssistant? TeachingAssistant { get; set; }
        public Subject Subject { get; set; } = null!;
        public System.Collections.Generic.ICollection<StudentAttendance> Attendances { get; set; } = new System.Collections.Generic.List<StudentAttendance>();
    }
}
