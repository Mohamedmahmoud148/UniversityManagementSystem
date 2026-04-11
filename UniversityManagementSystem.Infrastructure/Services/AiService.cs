using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Polly.Timeout;
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
    /// Resilience: Polly retry (3×, exponential back-off) + circuit-breaker
    /// (opens after 5 consecutive failures for 15 s).
    ///
    /// Endpoints consumed:
    ///   POST /api/chat        — conversational AI, intent detection, tool routing
    ///   POST /extract         — file/document data extraction
    ///   POST /generate-exam   — AI-generated exam question sets
    /// </summary>
    public class AiService(HttpClient httpClient, ILogger<AiService> logger) : IAiService
    {
        private readonly HttpClient _httpClient = httpClient;
        private readonly ILogger<AiService> _logger = logger;

        // ── Shared serializer options ─────────────────────────────────────────
        // snake_case output (matches FastAPI defaults), ignore nulls to keep payloads lean.
        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        // ── Polly resilience pipeline ─────────────────────────────────────────
        // Built once at class level — thread-safe, reused across all calls.
        private static readonly ResiliencePipeline _resiliencePipeline =
            new ResiliencePipelineBuilder()
                .AddRetry(new RetryStrategyOptions
                {
                    MaxRetryAttempts = 3,
                    Delay = TimeSpan.FromSeconds(1),
                    BackoffType = DelayBackoffType.Exponential,
                    UseJitter = true,
                    ShouldHandle = new PredicateBuilder()
                        .Handle<HttpRequestException>()
                        .Handle<TaskCanceledException>()
                })
                .AddCircuitBreaker(new CircuitBreakerStrategyOptions
                {
                    FailureRatio = 0.5,
                    MinimumThroughput = 5,
                    SamplingDuration = TimeSpan.FromSeconds(30),
                    BreakDuration = TimeSpan.FromSeconds(15),
                    OnOpened = args =>
                    {
                        // Circuit opened — AI service considered unavailable
                        return ValueTask.CompletedTask;
                    }
                })
                .AddTimeout(TimeSpan.FromSeconds(65)) // Slightly above HttpClient timeout
                .Build();

        // ── Fallback responses ────────────────────────────────────────────────
        private static readonly AiChatResponseDto _unavailableResponse = new()
        {
            Response = "I'm having trouble connecting to my brain right now. Please try again shortly.",
            IntentExecuted = "None"
        };

        // ----------------------------------------------------------------
        // Chat  —  POST /api/chat  (initial user message)
        // ----------------------------------------------------------------

        public async Task<AiChatResponseDto> SendChatMessageAsync(AiChatRequestDto request)
        {
            return await ExecuteChatAsync(request, "SendChatMessageAsync");
        }

        // ----------------------------------------------------------------
        // Chat  —  POST /api/chat  (tool result continuation)
        // ----------------------------------------------------------------

        /// <summary>
        /// Sends the backend's tool execution result back to the AI.
        /// Reuses the same /api/chat endpoint; FastAPI detects the tool_result
        /// field and generates a final natural-language response.
        /// </summary>
        public async Task<AiChatResponseDto> SendToolResultAsync(AiChatRequestDto request)
        {
            return await ExecuteChatAsync(request, "SendToolResultAsync");
        }

        // ----------------------------------------------------------------
        // Shared chat execution (with resilience pipeline)
        // ----------------------------------------------------------------

        private async Task<AiChatResponseDto> ExecuteChatAsync(AiChatRequestDto request, string callerName)
        {
            try
            {
                AiChatResponseDto? result = null;

                await _resiliencePipeline.ExecuteAsync(async ct =>
                {
                    var response = await _httpClient.PostAsJsonAsync("/api/chat", request, _jsonOptions, ct);
                    response.EnsureSuccessStatusCode();
                    result = await response.Content.ReadFromJsonAsync<AiChatResponseDto>(_jsonOptions, ct);
                });

                return result ?? _unavailableResponse;
            }
            catch (BrokenCircuitException ex)
            {
                _logger.LogWarning(ex, "[AiService] {Caller} – circuit open, AI service is unavailable.", callerName);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "[AiService] {Caller} – HTTP error after retries.", callerName);
            }
            catch (TimeoutRejectedException ex)
            {
                _logger.LogWarning(ex, "[AiService] {Caller} – request timed out after retries.", callerName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[AiService] {Caller} – unexpected error.", callerName);
            }

            return _unavailableResponse;
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
                _logger.LogError(ex, "[AiService] ExtractDataFromFileAsync – HTTP error.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[AiService] ExtractDataFromFileAsync – unexpected error.");
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
                return await response.Content.ReadFromJsonAsync<List<CreateExamQuestionDto>>(_jsonOptions) ?? [];
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "[AiService] GenerateExamAsync – HTTP error.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[AiService] GenerateExamAsync – unexpected error.");
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
                _logger.LogError(ex, "[AiService] AnalyzeTextAsync – HTTP error.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[AiService] AnalyzeTextAsync – unexpected error.");
            }

            return new AiResponseDto { Intent = "None", Confidence = 0 };
        }
    }
}
