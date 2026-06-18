using System;
using NUlid;

namespace UniversityManagementSystem.Core.Entities
{
    public class LectureSummary : BaseEntity
    {
        public Ulid RecordingId { get; set; }
        public string Summary { get; set; } = string.Empty;
        /// <summary>JSON array of key concept strings.</summary>
        public string KeyConceptsJson { get; set; } = "[]";
        /// <summary>JSON array of timeline sections: [{title, start, end}].</summary>
        public string TimelineJson { get; set; } = "[]";
        /// <summary>JSON array of suggested exam questions: [{question, difficulty}].</summary>
        public string SuggestedQuestionsJson { get; set; } = "[]";

        // Navigation
        public LectureRecording Recording { get; set; } = null!;
    }
}
