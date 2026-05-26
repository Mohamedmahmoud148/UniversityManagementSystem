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
    }
}
