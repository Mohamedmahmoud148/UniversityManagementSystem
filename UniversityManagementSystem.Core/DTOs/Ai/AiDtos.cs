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

        [JsonPropertyName("is_fallback")]
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

    // ── Essay AI grading ─────────────────────────────────────────────────────

    public class AiGradeEssayRequestDto
    {
        public string SubmissionText    { get; set; } = string.Empty;
        public string AssignmentTitle   { get; set; } = string.Empty;
        public string AssignmentDescription { get; set; } = string.Empty;
        public string? Rubric           { get; set; }
        public double MaxGrade          { get; set; } = 10;
    }

    public class AiGradeEssayResponseDto
    {
        [JsonPropertyName("score")]      public double Score      { get; set; }
        [JsonPropertyName("feedback")]   public string Feedback   { get; set; } = string.Empty;
        [JsonPropertyName("strengths")]  public string Strengths  { get; set; } = string.Empty;
        [JsonPropertyName("weaknesses")] public string Weaknesses { get; set; } = string.Empty;
        [JsonPropertyName("confidence")] public double Confidence { get; set; }
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

    // ── AI Companion Platform DTOs ────────────────────────────────────────────

    /// <summary>Single flashcard item returned by FastAPI.</summary>
    public class AiFlashcardItemDto
    {
        [JsonPropertyName("front")]      public string Front      { get; set; } = string.Empty;
        [JsonPropertyName("back")]       public string Back       { get; set; } = string.Empty;
        [JsonPropertyName("hint")]       public string? Hint      { get; set; }
        [JsonPropertyName("difficulty")] public string Difficulty { get; set; } = "medium";
    }

    public class AiStudyPlanRequestDto
    {
        public string StudentId     { get; set; } = string.Empty;
        public string StudentName   { get; set; } = string.Empty;
        public List<string> WeakSubjects  { get; set; } = new();
        public List<string> EnrolledSubjects { get; set; } = new();
        public double Gpa           { get; set; }
        public string Goal          { get; set; } = string.Empty;
        public string LearningStyle { get; set; } = "mixed";
        public int DaysUntilExam    { get; set; } = 14;
    }

    public class AiStudyPlanDto
    {
        [JsonPropertyName("plan_title")]   public string PlanTitle   { get; set; } = string.Empty;
        [JsonPropertyName("daily_tasks")]  public List<AiDailyTaskDto> DailyTasks { get; set; } = new();
        [JsonPropertyName("focus_areas")]  public List<string> FocusAreas { get; set; } = new();
        [JsonPropertyName("motivational_note")] public string MotivationalNote { get; set; } = string.Empty;
    }

    public class AiDailyTaskDto
    {
        [JsonPropertyName("day")]          public string Day         { get; set; } = string.Empty;
        [JsonPropertyName("subject")]      public string Subject     { get; set; } = string.Empty;
        [JsonPropertyName("task")]         public string Task        { get; set; } = string.Empty;
        [JsonPropertyName("duration_min")] public int DurationMin    { get; set; } = 30;
    }

    public class AiProgressReportRequestDto
    {
        public string StudentName   { get; set; } = string.Empty;
        public int SessionsThisWeek { get; set; }
        public double AvgAccuracy   { get; set; }
        public int StudyMinutes     { get; set; }
        public int StreakDays       { get; set; }
        public List<string> WeakSubjects  { get; set; } = new();
        public List<string> StrongSubjects { get; set; } = new();
        public string Period        { get; set; } = "weekly";  // "weekly" | "monthly"
    }

    // ── Study Session Questions ───────────────────────────────────────────────

    public class AiStudyQuestionDto
    {
        [JsonPropertyName("id")]             public string Id           { get; set; } = Guid.NewGuid().ToString();
        [JsonPropertyName("type")]           public string Type         { get; set; } = "mcq"; // "mcq" | "open"
        [JsonPropertyName("text")]           public string Text         { get; set; } = string.Empty;
        [JsonPropertyName("options")]        public List<string>? Options { get; set; }
        [JsonPropertyName("correct_answer")] public string CorrectAnswer { get; set; } = string.Empty;
        [JsonPropertyName("explanation")]    public string Explanation  { get; set; } = string.Empty;
    }

    public class AiGradeOpenAnswerResponseDto
    {
        [JsonPropertyName("score")]         public double Score        { get; set; }
        [JsonPropertyName("is_correct")]    public bool IsCorrect      { get; set; }
        [JsonPropertyName("feedback")]      public string Feedback     { get; set; } = string.Empty;
        [JsonPropertyName("explanation")]   public string Explanation  { get; set; } = string.Empty;
    }

}
