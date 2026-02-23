using System;
using System.Collections.Generic;

namespace UniversityManagementSystem.Core.Entities
{
    public class AttendanceSession : BaseEntity
    {
        public int SubjectId { get; set; }
        public DateTime SessionDate { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public string QrCodeContent { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;

        public int? DoctorId { get; set; }
        public int? TeachingAssistantId { get; set; }

        // Navigation Properties
        public Doctor? Doctor { get; set; }
        public TeachingAssistant? TeachingAssistant { get; set; }
        public Subject Subject { get; set; } = null!;
        public ICollection<StudentAttendance> Attendances { get; set; } = new List<StudentAttendance>();
    }
}
