using System;
using NUlid;

namespace UniversityManagementSystem.Core.Entities
{
    public class LectureFlashcard : BaseEntity
    {
        public Ulid RecordingId { get; set; }
        public string Front { get; set; } = string.Empty;
        public string Back { get; set; } = string.Empty;

        // Navigation
        public LectureRecording Recording { get; set; } = null!;
    }
}
