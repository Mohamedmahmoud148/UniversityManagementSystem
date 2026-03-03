using System.Net.Http.Json;
using System.Text.Json;
using UniversityManagementSystem.Core.DTOs.Ai;
using UniversityManagementSystem.Core.Interfaces;

namespace UniversityManagementSystem.Infrastructure.Services
{
    public class AiService(HttpClient httpClient) : IAiService
    {
        private readonly HttpClient _httpClient = httpClient;

        public async Task<AiResponseDto> AnalyzeTextAsync(string text)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync("/analyze", new { text });
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<AiResponseDto>() ?? new AiResponseDto();
                }
            }
            catch
            {
                // Log error
            }
            return new AiResponseDto { Intent = "None", Confidence = 0 };
        }

        public async Task<AiExtractResponseDto> ExtractDataFromFileAsync(string fileUrl, string fileType)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync("/extract", new { fileUrl, fileType });
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<AiExtractResponseDto>() ?? new AiExtractResponseDto { Success = false };
                }
            }
            catch
            {
                // Log error
            }
            return new AiExtractResponseDto { Success = false, Errors = ["Service unavailable"] };
        }

        public async Task<AiChatResponseDto> SendChatMessageAsync(AiChatRequestDto request)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync("/api/chat", request);
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<AiChatResponseDto>() ?? new AiChatResponseDto();
                }
            }
            catch
            {
                // Log error
            }
            return new AiChatResponseDto { response = "I'm having trouble connecting to my brain right now.", intent_executed = "None" };
        }
    }
}
