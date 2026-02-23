using System.Net.Http.Json;
using System.Text.Json;
using UniversityManagementSystem.Core.DTOs.Ai;
using UniversityManagementSystem.Core.Interfaces;

namespace UniversityManagementSystem.Infrastructure.Services
{
    public class AiService(HttpClient httpClient) : IAiService
    {
        private readonly HttpClient _httpClient = httpClient;
        private const string AiBaseUrl = "http://localhost:8000"; // Placeholder

        public async Task<AiResponseDto> AnalyzeTextAsync(string text)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync($"{AiBaseUrl}/analyze", new { text });
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
                var response = await _httpClient.PostAsJsonAsync($"{AiBaseUrl}/extract", new { fileUrl, fileType });
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

        public async Task<AiChatResponseDto> SendChatMessageAsync(string message, string sessionId, string historyContext)
        {
            try
            {
                var payload = new { message, sessionId, history = historyContext };
                var response = await _httpClient.PostAsJsonAsync($"{AiBaseUrl}/chat", payload);
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<AiChatResponseDto>() ?? new AiChatResponseDto();
                }
            }
            catch
            {
                // Log error
            }
            return new AiChatResponseDto { Reply = "I'm having trouble connecting to my brain right now.", SuggestedAction = "None" };
        }
    }
}
