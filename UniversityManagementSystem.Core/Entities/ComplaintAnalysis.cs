using NUlid;

namespace UniversityManagementSystem.Core.Entities
{
    public class ComplaintAnalysis : BaseEntity
    {
        public Ulid ComplaintId { get; set; }
        public double SentimentScore { get; set; }
        
        /// <summary>e.g., teaching_quality, exam_difficulty, technical_issue, harassment</summary>
        public string Category { get; set; } = string.Empty;
        
        /// <summary>e.g., low, medium, high, critical</summary>
        public string Severity { get; set; } = string.Empty;
        
        /// <summary>AI-generated summary</summary>
        public string AiSummary { get; set; } = string.Empty;
        
        /// <summary>Points to a ComplaintCluster if clustered</summary>
        public string? DuplicateGroupId { get; set; }
        
        public string SuggestedAction { get; set; } = string.Empty;

        public Complaint Complaint { get; set; } = null!;
    }
}
