using System;
using System.Collections.Generic;
using NUlid;

namespace UniversityManagementSystem.Core.DTOs
{
    public class CreateAttendanceSessionDto
    {
        public Ulid  SubjectId         { get; set; }
        public Ulid? SubjectOfferingId { get; set; }
        public DateTime SessionDate { get; set; }
        public TimeSpan StartTime   { get; set; }
        public TimeSpan EndTime     { get; set; }
    }

    public class QrCodeResponseDto
    {
        public Ulid SessionId { get; set; }
        public string QrContent { get; set; } = string.Empty;
        public string QrImageUrl { get; set; } = string.Empty;
    }

    public class RecordAttendanceDto
    {
        public Ulid SessionId { get; set; }
        public string QrContent { get; set; } = string.Empty;
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
    }

    public class AttendanceResponseDto
    {
        public Ulid SessionId { get; set; }
        public Ulid StudentId { get; set; }
        public string StudentName { get; set; } = string.Empty;
        public DateTime? CheckInTime { get; set; }
        public bool IsPresent { get; set; }
    }

    public class UpdateAttendanceDto
    {
        public bool IsPresent { get; set; }
    }
}
