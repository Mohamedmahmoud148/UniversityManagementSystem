using System.Text.Json.Serialization;
using NUlid;

namespace UniversityManagementSystem.Core.DTOs.Ai
{
    // ── Existing response types ──────────────────────────────────────────────

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

    // ── Chat response from FastAPI (snake_case wire → PascalCase C#) ─────────
    /// <summary>
    /// Deserialized from FastAPI's snake_case JSON response.
    /// [JsonPropertyName] binds wire names; C# properties use PascalCase.
    /// </summary>
    public class AiChatResponseDto
    {
        [JsonPropertyName("response")]
        public string Response { get; set; } = string.Empty;

        [JsonPropertyName("intent_executed")]
        public string IntentExecuted { get; set; } = string.Empty;

        [JsonPropertyName("tool_used")]
        public string? ToolUsed { get; set; }

        [JsonPropertyName("model_used")]
        public string ModelUsed { get; set; } = string.Empty;

        [JsonPropertyName("metadata")]
        public System.Text.Json.JsonElement? Metadata { get; set; }

        [JsonPropertyName("suggestions")]
        public List<string>? Suggestions { get; set; }

        [JsonPropertyName("actions_available")]
        public List<string>? ActionsAvailable { get; set; }

        [JsonIgnore]
        public bool IsFallback { get; set; }
    }

    // ── Chat request to FastAPI (PascalCase C# → snake_case wire via policy) ─
    /// <summary>
    /// Sent to POST /api/chat on the FastAPI service.
    /// <see cref="AiService"/> uses JsonNamingPolicy.SnakeCaseLower, so PascalCase
    /// properties are automatically serialized as snake_case on the wire.
    /// </summary>
    public class AiChatRequestDto
    {
        public string UserId { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public object[] History { get; set; } = [];
        public object AcademicContext { get; set; } = new { };
        public string ConversationId { get; set; } = string.Empty;

        /// <summary>
        /// Populated on the second AI call (after tool execution).
        /// Null on the initial user-message call.
        /// FastAPI reads this to know the conversation is in tool-result phase.
        /// </summary>
        public AiToolCallResult? ToolResult { get; set; }
    }

    // ── Tool result envelope sent back to AI ─────────────────────────────────
    /// <summary>
    /// Standardised format: { "tool": "ToolName", "result": { ... } }
    /// This is what the backend sends to the AI after executing the tool.
    /// </summary>
    public class AiToolCallResult
    {
        public string Tool { get; set; } = string.Empty;
        public object? Result { get; set; }
    }

    // ── Exam generation ──────────────────────────────────────────────────────

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
