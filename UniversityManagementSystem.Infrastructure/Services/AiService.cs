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
    public class AiService(HttpClient httpClient, ILogger<AiService> logger, Microsoft.AspNetCore.Http.IHttpContextAccessor httpContextAccessor) : IAiService
    {
        private readonly HttpClient _httpClient = httpClient;
        private readonly ILogger<AiService> _logger = logger;
        private readonly Microsoft.AspNetCore.Http.IHttpContextAccessor _httpContextAccessor = httpContextAccessor;

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
            IntentExecuted = "None",
            IsFallback = true
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

        private string? GetAuthHeader() =>
            _httpContextAccessor.HttpContext?.Request.Headers.Authorization.FirstOrDefault();

        private async Task<AiChatResponseDto> ExecuteChatAsync(AiChatRequestDto request, string callerName)
        {
            var authHeader = GetAuthHeader();

            try
            {
                AiChatResponseDto? result = null;

                await _resiliencePipeline.ExecuteAsync(async ct =>
                {
                    // Create a new request message per call — safe under concurrent use.
                    using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/chat");
                    httpRequest.Content = JsonContent.Create(request, options: _jsonOptions);
                    if (!string.IsNullOrEmpty(authHeader))
                        httpRequest.Headers.TryAddWithoutValidation("Authorization", authHeader);

                    var response = await _httpClient.SendAsync(httpRequest, ct);
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
        // Essay AI Grading  —  POST /api/ai/grade-submission
        // ----------------------------------------------------------------

        public async Task<AiGradeEssayResponseDto?> GradeEssayAsync(AiGradeEssayRequestDto request)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync("/api/ai/grade-submission", request, _jsonOptions);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadFromJsonAsync<AiGradeEssayResponseDto>(_jsonOptions);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "[AiService] GradeEssayAsync – HTTP error.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[AiService] GradeEssayAsync – unexpected error.");
            }

            return null;
        }

        // ----------------------------------------------------------------
        // RAG Indexing  —  POST /api/rag/index
        // ----------------------------------------------------------------

        public async Task IndexMaterialAsync(string materialId, string fileUrl, string contentType, string title, string offeringId)
        {
            try
            {
                var payload = new
                {
                    material_id = materialId,
                    file_url    = fileUrl,
                    metadata    = new
                    {
                        materialTitle = title,
                        offeringId    = offeringId,
                        contentType   = contentType,
                    }
                };

                var response = await _httpClient.PostAsJsonAsync("/api/rag/index", payload, _jsonOptions);
                if (response.IsSuccessStatusCode)
                    _logger.LogInformation("[AiService] IndexMaterialAsync – material {MaterialId} indexed successfully.", materialId);
                else
                    _logger.LogWarning("[AiService] IndexMaterialAsync – material {MaterialId} returned HTTP {Status}.", materialId, (int)response.StatusCode);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "[AiService] IndexMaterialAsync – HTTP error for material {MaterialId}.", materialId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[AiService] IndexMaterialAsync – unexpected error for material {MaterialId}.", materialId);
            }
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

        // ----------------------------------------------------------------
        // Complaint Analysis  —  POST /api/ai/analyze-complaint
        // ----------------------------------------------------------------

        public async Task<AiComplaintAnalysisResponseDto> AnalyzeComplaintAsync(AiAnalyzeComplaintRequestDto request)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync("/api/ai/analyze-complaint", request, _jsonOptions);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadFromJsonAsync<AiComplaintAnalysisResponseDto>(_jsonOptions) ?? new AiComplaintAnalysisResponseDto();
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "[AiService] AnalyzeComplaintAsync – HTTP error.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[AiService] AnalyzeComplaintAsync – unexpected error.");
            }

            return new AiComplaintAnalysisResponseDto();
        }

        // ----------------------------------------------------------------
        // AI Companion: Flashcard Generation  —  POST /api/companion/generate-flashcards
        // ----------------------------------------------------------------

        public async Task<List<AiFlashcardItemDto>> GenerateFlashcardsAsync(
            string topicName, int cardCount, string difficulty)
        {
            try
            {
                var payload = new { topic = topicName, card_count = cardCount, difficulty };
                var response = await _httpClient.PostAsJsonAsync(
                    "/api/companion/generate-flashcards", payload, _jsonOptions);
                response.EnsureSuccessStatusCode();
                var result = await response.Content
                    .ReadFromJsonAsync<List<AiFlashcardItemDto>>(_jsonOptions);
                return result ?? [];
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[AiService] GenerateFlashcardsAsync – error for topic {Topic}.", topicName);
                return [];
            }
        }

        // ----------------------------------------------------------------
        // AI Companion: Quick Prompt  —  POST /api/companion/quick-prompt
        // ----------------------------------------------------------------

        public async Task<string?> SendQuickPromptAsync(string prompt)
        {
            try
            {
                var payload = new { prompt };
                var response = await _httpClient.PostAsJsonAsync(
                    "/api/companion/quick-prompt", payload, _jsonOptions);
                response.EnsureSuccessStatusCode();
                var result = await response.Content
                    .ReadFromJsonAsync<Dictionary<string, string>>(_jsonOptions);
                return result?.GetValueOrDefault("response");
            }
            catch (Exception ex)
            {
                _logger.LogWarning("[AiService] SendQuickPromptAsync – error: {Error}", ex.Message);
                return null;
            }
        }

        // ----------------------------------------------------------------
        // AI Companion: Study Plan  —  POST /api/companion/study-plan
        // ----------------------------------------------------------------

        public async Task<AiStudyPlanDto?> GenerateStudyPlanAsync(AiStudyPlanRequestDto request)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync(
                    "/api/companion/study-plan", request, _jsonOptions);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadFromJsonAsync<AiStudyPlanDto>(_jsonOptions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[AiService] GenerateStudyPlanAsync – error.");
                return null;
            }
        }

        // ----------------------------------------------------------------
        // AI Companion: Progress Report  —  POST /api/companion/progress-report
        // ----------------------------------------------------------------

        public async Task<string?> GenerateProgressReportAsync(AiProgressReportRequestDto request)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync(
                    "/api/companion/progress-report", request, _jsonOptions);
                response.EnsureSuccessStatusCode();
                var result = await response.Content
                    .ReadFromJsonAsync<Dictionary<string, string>>(_jsonOptions);
                return result?.GetValueOrDefault("report");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[AiService] GenerateProgressReportAsync – error.");
                return null;
            }
        }
    }
}
