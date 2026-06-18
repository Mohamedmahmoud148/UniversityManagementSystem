using System;
using NUlid;

namespace UniversityManagementSystem.Core.Entities
{
    public enum LectureRecordingStatus
    {
        Uploading,
        Transcribing,
        Analyzing,
        Completed,
        Failed
    }

    /// <summary>
    /// Student-uploaded lecture audio recording.
    /// Processing pipeline: Upload → Transcribe → Analyze → Completed.
    /// </summary>
    public class LectureRecording : BaseEntity
    {
        public Ulid StudentId { get; set; }

        public string FileName { get; set; } = string.Empty;
        public string OriginalFileName { get; set; } = string.Empty;
        public string StoragePath { get; set; } = string.Empty;
        public string MimeType { get; set; } = string.Empty;
        public long FileSize { get; set; }

        public int? DurationSeconds { get; set; }
        public LectureRecordingStatus Status { get; set; } = LectureRecordingStatus.Uploading;
        public string? ErrorMessage { get; set; }
        public int TranscriptChars { get; set; }
        public DateTime? ProcessedAt { get; set; }

        // Navigation
        public Student Student { get; set; } = null!;
        public LectureSummary? Summary { get; set; }
    }
}
