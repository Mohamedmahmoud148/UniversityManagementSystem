using System;
using NUlid;

namespace UniversityManagementSystem.Core.Events
{
    public class AttendanceRecordedEvent
    {
        public Ulid StudentId { get; set; }
        public Ulid SessionId { get; set; }
        public string QrContent { get; set; } = string.Empty;
        public DateTime CheckInTime { get; set; } = DateTime.UtcNow;
    }
}
