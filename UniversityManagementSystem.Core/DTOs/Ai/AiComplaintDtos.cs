using System.Collections.Generic;

namespace UniversityManagementSystem.Core.DTOs.Ai
{
    public class AiAnalyzeComplaintRequestDto
    {
        public string ComplaintId { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string TargetType { get; set; } = string.Empty;
        public string TargetId { get; set; } = string.Empty;
    }

    public class AiComplaintAnalysisResponseDto
    {
        public double SentimentScore { get; set; }
        public string Category { get; set; } = string.Empty;
        public string Severity { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
        public string DuplicateGroupId { get; set; } = string.Empty;
        public string RecommendedAction { get; set; } = string.Empty;
    }
}
