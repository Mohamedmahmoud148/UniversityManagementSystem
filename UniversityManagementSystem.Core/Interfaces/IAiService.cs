using System.Threading.Tasks;
using UniversityManagementSystem.Core.DTOs.Ai;

namespace UniversityManagementSystem.Core.Interfaces
{
    public interface IAiService
    {
        Task<AiResponseDto> AnalyzeTextAsync(string text);
        Task<AiExtractResponseDto> ExtractDataFromFileAsync(string fileUrl, string fileType);
        Task<AiChatResponseDto> SendChatMessageAsync(AiChatRequestDto request);
        Task<System.Collections.Generic.List<UniversityManagementSystem.Core.DTOs.CreateExamQuestionDto>> GenerateExamAsync(int subjectOfferingId);
    }
}
