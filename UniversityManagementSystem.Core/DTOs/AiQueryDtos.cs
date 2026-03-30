using System.Collections.Generic;

namespace UniversityManagementSystem.Core.DTOs
{
    /// <summary>Request body for POST /api/AI/summarize</summary>
    public class SummarizeRequestDto
    {
        public string FileId { get; set; } = string.Empty;
    }

    /// <summary>Request body for POST /api/AI/ask</summary>
    public class AskRequestDto
    {
        public string FileId { get; set; } = string.Empty;
        public string Question { get; set; } = string.Empty;
    }

    /// <summary>Generic AI query response.</summary>
    public class AiQueryResponseDto
    {
        public string Result { get; set; } = string.Empty;
        public string FileId { get; set; } = string.Empty;
        public bool UsedExtractedText { get; set; }
    }
}
