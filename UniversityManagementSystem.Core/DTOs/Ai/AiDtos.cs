namespace UniversityManagementSystem.Core.DTOs.Ai
{
    public class AiResponseDto
    {
        public string Intent { get; set; } = string.Empty;
        public double Confidence { get; set; }
        public object? Data { get; set; }
    }

    public class AiExtractResponseDto
    {
        public bool Success { get; set; }
        public string ExtractectedJson { get; set; } = string.Empty;
        public string[] Errors { get; set; } = [];
    }

    public class AiChatResponseDto
    {
        public string Reply { get; set; } = string.Empty;
        public string SuggestedAction { get; set; } = string.Empty;
    }
}
