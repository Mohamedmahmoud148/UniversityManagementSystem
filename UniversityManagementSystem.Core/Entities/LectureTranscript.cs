using System;
using NUlid;

namespace UniversityManagementSystem.Core.Entities
{
    /// <summary>
    /// One chunk of the lecture transcript with optional timestamps.
    /// Chunked for embedding + Q&A retrieval.
    /// </summary>
    public class LectureTranscript : BaseEntity
    {
        public Ulid RecordingId { get; set; }
        public int ChunkIndex { get; set; }
        public string Text { get; set; } = string.Empty;
        public int? StartSecond { get; set; }
        public int? EndSecond { get; set; }
        public string? EmbeddingId { get; set; }

        // Navigation
        public LectureRecording Recording { get; set; } = null!;
    }
}
