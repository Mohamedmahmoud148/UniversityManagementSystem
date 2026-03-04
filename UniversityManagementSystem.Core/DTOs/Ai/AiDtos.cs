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
        public string response { get; set; } = string.Empty;
        public string intent_executed { get; set; } = string.Empty;
        public string tool_used { get; set; } = string.Empty;
        public string model_used { get; set; } = string.Empty;
        public object? metadata { get; set; }
    }

    public class AiChatRequestDto
    {
        public int user_id { get; set; }
        public string role { get; set; } = string.Empty;
        public string message { get; set; } = string.Empty;
        public object[] history { get; set; } = [];
        public object academic_context { get; set; } = new { };
        public string conversation_id { get; set; } = string.Empty;
    }

    public class AiGenerateExamRequestDto
    {
        public string Subject { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
        public string Batch { get; set; } = string.Empty;
        public string Difficulty { get; set; } = "Medium";
        public int QuestionCount { get; set; } = 10;
        public string ExamType { get; set; } = "Final";
        public System.Collections.Generic.List<string> Topics { get; set; } = new();
    }
}
