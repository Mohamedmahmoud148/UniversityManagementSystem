using System;

namespace UniversityManagementSystem.Core.Events
{
    public class AttendanceRecordedEvent
    {
        public int StudentId { get; set; }
        public int SessionId { get; set; }
        public string QrContent { get; set; } = string.Empty;
        public DateTime CheckInTime { get; set; } = DateTime.UtcNow;
    }
}
