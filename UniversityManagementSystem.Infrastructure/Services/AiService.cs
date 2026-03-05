using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using UniversityManagementSystem.Core.DTOs;
using UniversityManagementSystem.Core.DTOs.Ai;
using UniversityManagementSystem.Core.Interfaces;

namespace UniversityManagementSystem.Infrastructure.Services
{
    /// <summary>
    /// Single centralized gateway for all communication with the external
    /// FastAPI AI Orchestration Service.
    ///
    /// Base URL is configured via the AI_SERVICE_URL environment variable
    /// (registered in Program.cs through AddHttpClient&lt;IAiService, AiService&gt;).
    ///
    /// Endpoints consumed:
    ///   POST /api/chat        — conversational AI, intent detection, tool routing
    ///   POST /extract         — file/document data extraction
    ///   POST /generate-exam   — AI-generated exam question sets
    /// </summary>
    public class AiService(HttpClient httpClient) : IAiService
    {
        private readonly HttpClient _httpClient = httpClient;

        // Shared serializer options: snake_case output (matches FastAPI defaults),
        // ignore null values to keep payloads lean.
        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        // ----------------------------------------------------------------
        // Chat  —  POST /api/chat
        // ----------------------------------------------------------------

        public async Task<AiChatResponseDto> SendChatMessageAsync(AiChatRequestDto request)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync("/api/chat", request, _jsonOptions);

                response.EnsureSuccessStatusCode();

                return await response.Content.ReadFromJsonAsync<AiChatResponseDto>(_jsonOptions)
                       ?? new AiChatResponseDto
                       {
                           response = "The AI returned an empty response.",
                           intent_executed = "None"
                       };
            }
            catch (HttpRequestException ex)
            {
                // Network-level failure (DNS, connection refused, timeout).
                Console.Error.WriteLine($"[AiService] SendChatMessageAsync – HTTP error: {ex.Message}");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[AiService] SendChatMessageAsync – Unexpected error: {ex.Message}");
            }

            return new AiChatResponseDto
            {
                response = "I'm having trouble connecting to my brain right now. Please try again shortly.",
                intent_executed = "None"
            };
        }

        // ----------------------------------------------------------------
        // File Extraction  —  POST /extract
        // ----------------------------------------------------------------

        public async Task<AiExtractResponseDto> ExtractDataFromFileAsync(string fileUrl, string fileType)
        {
            try
            {
                var payload = new { file_url = fileUrl, file_type = fileType };

                var response = await _httpClient.PostAsJsonAsync("/extract", payload, _jsonOptions);

                response.EnsureSuccessStatusCode();

                return await response.Content.ReadFromJsonAsync<AiExtractResponseDto>(_jsonOptions)
                       ?? new AiExtractResponseDto { Success = false, Errors = ["Empty response from AI service."] };
            }
            catch (HttpRequestException ex)
            {
                Console.Error.WriteLine($"[AiService] ExtractDataFromFileAsync – HTTP error: {ex.Message}");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[AiService] ExtractDataFromFileAsync – Unexpected error: {ex.Message}");
            }

            return new AiExtractResponseDto { Success = false, Errors = ["AI extraction service is unavailable."] };
        }

        // ----------------------------------------------------------------
        // Exam Generation  —  POST /generate-exam
        // ----------------------------------------------------------------

        public async Task<List<CreateExamQuestionDto>> GenerateExamAsync(AiGenerateExamRequestDto request)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync("/generate-exam", request, _jsonOptions);

                response.EnsureSuccessStatusCode();

                return await response.Content.ReadFromJsonAsync<List<CreateExamQuestionDto>>(_jsonOptions)
                       ?? [];
            }
            catch (HttpRequestException ex)
            {
                Console.Error.WriteLine($"[AiService] GenerateExamAsync – HTTP error: {ex.Message}");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[AiService] GenerateExamAsync – Unexpected error: {ex.Message}");
            }

            return [];
        }

        // ----------------------------------------------------------------
        // Text Analysis  —  POST /analyze  (internal / legacy)
        // ----------------------------------------------------------------

        public async Task<AiResponseDto> AnalyzeTextAsync(string text)
        {
            try
            {
                var payload = new { text };

                var response = await _httpClient.PostAsJsonAsync("/analyze", payload, _jsonOptions);

                response.EnsureSuccessStatusCode();

                return await response.Content.ReadFromJsonAsync<AiResponseDto>(_jsonOptions)
                       ?? new AiResponseDto { Intent = "None", Confidence = 0 };
            }
            catch (HttpRequestException ex)
            {
                Console.Error.WriteLine($"[AiService] AnalyzeTextAsync – HTTP error: {ex.Message}");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[AiService] AnalyzeTextAsync – Unexpected error: {ex.Message}");
            }

            return new AiResponseDto { Intent = "None", Confidence = 0 };
        }
    }
}
