using System.Threading.Tasks;
using UniversityManagementSystem.Core.DTOs.Ai;

namespace UniversityManagementSystem.Core.Interfaces
{
    public interface IAiService
    {
        Task<AiResponseDto> AnalyzeTextAsync(string text);
        Task<AiExtractResponseDto> ExtractDataFromFileAsync(string fileUrl, string fileType);

        // Analyze student complaint
        Task<AiComplaintAnalysisResponseDto> AnalyzeComplaintAsync(AiAnalyzeComplaintRequestDto request);

        /// <summary>
        /// Sends a user message to the AI and returns the AI's initial response,
        /// which may include a <c>ToolUsed</c> signals requiring backend tool execution.
        /// </summary>
        Task<AiChatResponseDto> SendChatMessageAsync(AiChatRequestDto request);

        /// <summary>
        /// Sends a tool execution result back to the AI and gets the final
        /// natural-language response to return to the user.
        /// Reuses POST /api/chat with the ToolResult field populated.
        /// </summary>
        Task<AiChatResponseDto> SendToolResultAsync(AiChatRequestDto request);

        Task<System.Collections.Generic.List<UniversityManagementSystem.Core.DTOs.CreateExamQuestionDto>> GenerateExamAsync(AiGenerateExamRequestDto request);

        /// <summary>Sends an essay answer to FastAPI for AI grading. Returns score + feedback.</summary>
        Task<AiGradeEssayResponseDto?> GradeEssayAsync(AiGradeEssayRequestDto request);

        /// <summary>
        /// Fire-and-forget: asks the AI service to chunk, embed, and index a material
        /// so it becomes searchable via RAG. Does not throw — logs internally.
        /// </summary>
        Task IndexMaterialAsync(string materialId, string fileUrl, string contentType, string title, string offeringId);

        // ── AI Companion Platform additions ───────────────────────────────

        /// <summary>
        /// Generate AI flashcards for a topic.
        /// Returns a list of (Front, Back, Hint, Difficulty) objects.
        /// </summary>
        Task<System.Collections.Generic.List<AiFlashcardItemDto>> GenerateFlashcardsAsync(
            string topicName, int cardCount, string difficulty);

        /// <summary>
        /// Send a one-shot prompt to the AI and get a text response.
        /// Used for feedback generation, recommendations, and summaries.
        /// Does not require conversation context.
        /// </summary>
        Task<string?> SendQuickPromptAsync(string prompt);

        /// <summary>
        /// Generate a personalized study plan for a student based on their profile.
        /// </summary>
        Task<AiStudyPlanDto?> GenerateStudyPlanAsync(AiStudyPlanRequestDto request);

        /// <summary>
        /// Generate study session questions (MCQ or open-ended) for a given topic/type/difficulty.
        /// </summary>
        Task<List<DTOs.Ai.AiStudyQuestionDto>> GenerateStudyQuestionsAsync(
            string topic, string sessionType, string difficulty, int count);

        /// <summary>
        /// Grade a single open-ended answer against the correct answer.
        /// Returns score 0-100 + explanation + feedback.
        /// </summary>
        Task<DTOs.Ai.AiGradeOpenAnswerResponseDto?> GradeOpenAnswerAsync(
            string question, string studentAnswer, string topic, string difficulty);

        /// <summary>
        /// Generate an academic progress report for a student.
        /// </summary>
        Task<string?> GenerateProgressReportAsync(AiProgressReportRequestDto request);
    }
}
