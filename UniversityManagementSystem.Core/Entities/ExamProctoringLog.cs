using System;
using NUlid;

namespace UniversityManagementSystem.Core.Entities
{
    public enum ProctoringStatus { Clean, Suspicious, Flagged }

    public class ExamProctoringLog : BaseEntity
    {
        public Ulid ExamSubmissionId { get; set; }
        public Ulid StudentId { get; set; }
        public Ulid ExamId { get; set; }
        public int TabSwitchCount { get; set; } = 0;
        public int FullscreenExitCount { get; set; } = 0;
        public int SuspiciousActivityCount { get; set; } = 0;
        /// <summary>JSON array of {type, timestamp, details} events.</summary>
        public string EventsJson { get; set; } = "[]";
        public ProctoringStatus Status { get; set; } = ProctoringStatus.Clean;
        public string? DoctorNote { get; set; }

        // Navigation Properties
        public ExamSubmission ExamSubmission { get; set; } = null!;
        public Student Student { get; set; } = null!;
    }
}
