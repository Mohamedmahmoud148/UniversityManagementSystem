using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NUlid;
using UniversityManagementSystem.Core.Application.AI.Execution;
using UniversityManagementSystem.Core.Application.AI.Security;
using UniversityManagementSystem.Core.DTOs;
using UniversityManagementSystem.Core.DTOs.Ai;
using UniversityManagementSystem.Core.Entities;
using UniversityManagementSystem.Core.Interfaces;
using UniversityManagementSystem.Infrastructure.Data;

namespace UniversityManagementSystem.Infrastructure.Services
{
    /// <summary>
    /// Implements the full AI agentic loop:
    ///   User → AI (initial call) → [optional] Backend Tool → AI (continuation call) → User
    /// </summary>
    public class ChatService(
        AppDbContext context,
        IAiService aiService,
        AiToolRegistry toolRegistry,
        ILogger<ChatService> logger) : IChatService
    {
        private readonly AppDbContext _context = context;
        private readonly IAiService _aiService = aiService;
        private readonly AiToolRegistry _toolRegistry = toolRegistry;
        private readonly ILogger<ChatService> _logger = logger;

        // ── Create Conversation ──────────────────────────────────────────────

        public async Task<Ulid> CreateConversationAsync(Ulid userId, string title)
        {
            var conversation = new Conversation
            {
                UserId = userId,
                Title = title,
                IsActive = true
            };
            _context.Conversations.Add(conversation);
            await _context.SaveChangesAsync();
            return conversation.Id;
        }

        // ── Fetch Messages ───────────────────────────────────────────────────

        public async Task<IEnumerable<ChatResponseDto>> GetConversationMessagesAsync(Ulid conversationId)
        {
            return await _context.ChatMessages
                .Where(m => m.ConversationId == conversationId)
                .OrderBy(m => m.CreatedAt)
                .Select(m => new ChatResponseDto
                {
                    Id = m.Id,
                    Content = m.Content,
                    IsUserMessage = m.IsUserMessage,
                    SentAt = m.CreatedAt
                })
                .ToListAsync();
        }

        // ── Fetch Conversations ──────────────────────────────────────────────

        public async Task<IEnumerable<ConversationDto>> GetUserConversationsAsync(Ulid userId)
        {
            return await _context.Conversations
               .Where(c => c.UserId == userId && c.IsActive)
               .Select(c => new ConversationDto
               {
                   Id = c.Id,
                   Title = c.Title,
                   LastMessageAt = c.Messages
                       .OrderByDescending(m => m.CreatedAt)
                       .Select(m => m.CreatedAt)
                       .FirstOrDefault()
               })
               .ToListAsync();
        }

        // ── Send Message — Full AI Agentic Loop ──────────────────────────────

        public async Task<ChatResponseDto> SendMessageAsync(
            Ulid userId,
            SendMessageDto messageDto,
            string role,
            ClaimsPrincipal caller)
        {
            // ── 0. Validate Conversation ──────────────────────────────────────
            var conversation = await _context.Conversations.FindAsync(messageDto.ConversationId);
            if (conversation == null || conversation.UserId != userId)
                throw new KeyNotFoundException("Conversation not found or access denied.");

            // ── 1. Fetch history BEFORE saving the current message ────────────
            //  (Fetching after would include the user's own message in history)
            var history = await _context.ChatMessages
                .Where(m => m.ConversationId == messageDto.ConversationId)
                .OrderByDescending(m => m.CreatedAt)
                .Take(10)
                .OrderBy(m => m.CreatedAt)          // Restore chronological order
                .Select(m => new
                {
                    role    = m.IsUserMessage ? "user" : "assistant",
                    content = m.Content
                })
                .ToListAsync();

            // ── 2. Save User Message ──────────────────────────────────────────
            var userMsg = new ChatMessage
            {
                ConversationId = messageDto.ConversationId,
                Content        = messageDto.Content,
                IsUserMessage  = true,
                CreatedAt      = DateTime.UtcNow
            };
            _context.ChatMessages.Add(userMsg);
            await _context.SaveChangesAsync();

            // ── 3. Build AI request with full context ─────────────────────────
            if (string.IsNullOrWhiteSpace(role))
            {
                _logger.LogWarning("[ChatService] SendMessageAsync: role is empty for userId={UserId}", userId);
                role = "student";
            }

            var aiRequest = new AiChatRequestDto
            {
                UserId          = userId,
                Role            = role.ToLower(),
                Message         = messageDto.Content,
                History         = history.Cast<object>().ToArray(),
                AcademicContext = new { },
                ConversationId  = conversation.Id.ToString()
            };

            // ── 4. Initial AI Call ────────────────────────────────────────────
            _logger.LogDebug("[ChatService] Calling AI for userId={UserId} conversationId={ConvId}",
                userId, conversation.Id);

            var aiResponse = await _aiService.SendChatMessageAsync(aiRequest);

            // ── 5. Tool Execution Loop ────────────────────────────────────────
            string finalResponseText = aiResponse.Response;
            string finalIntent       = aiResponse.IntentExecuted;

            if (!string.IsNullOrWhiteSpace(aiResponse.ToolUsed))
            {
                finalResponseText = await ExecuteToolAndGetFinalResponseAsync(
                    aiResponse, aiRequest, caller, role, userId, conversation.Id.ToString());

                // After tool execution, intent is the tool name
                finalIntent = aiResponse.ToolUsed;
            }

            // ── 6. Save AI Response ───────────────────────────────────────────
            var aiMsg = new ChatMessage
            {
                ConversationId = messageDto.ConversationId,
                Content        = finalResponseText,
                IsUserMessage  = false,
                Intent         = finalIntent,
                CreatedAt      = DateTime.UtcNow
            };
            _context.ChatMessages.Add(aiMsg);
            await _context.SaveChangesAsync();

            // ── 7. Return Final Response ──────────────────────────────────────
            return new ChatResponseDto
            {
                Id            = aiMsg.Id,
                Content       = aiMsg.Content,
                IsUserMessage = false,
                SentAt        = aiMsg.CreatedAt
            };
        }

        // ── Tool Execution ───────────────────────────────────────────────────

        private async Task<string> ExecuteToolAndGetFinalResponseAsync(
            AiChatResponseDto aiResponse,
            AiChatRequestDto originalRequest,
            ClaimsPrincipal caller,
            string role,
            Ulid userId,
            string conversationId)
        {
            var toolName = aiResponse.ToolUsed!;

            // ── 5a. Validate against capability matrix ────────────────────────
            if (!AiCapabilityMatrix.IsAllowed(role, toolName))
            {
                _logger.LogWarning(
                    "[ChatService] Role '{Role}' is not allowed to use tool '{Tool}'. Blocked.",
                    role, toolName);

                return $"I'm sorry, your role ({role}) doesn't have permission to execute the '{toolName}' action.";
            }

            // ── 5b. Resolve tool from registry ────────────────────────────────
            var tool = _toolRegistry.GetTool(toolName);
            if (tool == null)
            {
                _logger.LogWarning("[ChatService] Tool '{Tool}' requested by AI but not registered.", toolName);
                return $"I wanted to help with '{toolName}', but that capability isn't available yet.";
            }

            // ── 5c. Extract parameters from metadata (defensive, null-safe) ───
            object parameters;
            try
            {
                parameters = ExtractToolParameters(aiResponse.Metadata, toolName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "[ChatService] Failed to extract parameters for tool '{Tool}' from metadata.", toolName);
                return "I couldn't process the required information to complete that action. Please try rephrasing your request.";
            }

            // ── 5d. Execute the tool ──────────────────────────────────────────
            object? toolResult;
            try
            {
                _logger.LogInformation(
                    "[ChatService] Executing tool '{Tool}' for userId={UserId} role={Role}",
                    toolName, userId, role);

                toolResult = await tool.ExecuteAsync(parameters, caller);

                _logger.LogInformation("[ChatService] Tool '{Tool}' executed successfully.", toolName);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "[ChatService] Unauthorized tool execution for tool '{Tool}'.", toolName);
                return "You don't have permission to perform that action.";
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "[ChatService] Invalid parameters for tool '{Tool}'.", toolName);
                return $"I couldn't complete that action due to invalid parameters: {ex.Message}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ChatService] Tool '{Tool}' execution failed.", toolName);
                return "Something went wrong while processing your request. Please try again later.";
            }

            // ── 5e. Build tool result envelope ────────────────────────────────
            var toolResultEnvelope = new AiToolCallResult
            {
                Tool   = toolName,
                Result = toolResult
            };

            // ── 5f. Send tool result back to AI for final natural-language response
            var continuationRequest = new AiChatRequestDto
            {
                UserId          = originalRequest.UserId,
                Role            = originalRequest.Role,
                ConversationId  = conversationId,
                Message         = originalRequest.Message,   // Keep original user intent for context
                History         = originalRequest.History,
                AcademicContext = originalRequest.AcademicContext,
                ToolResult      = toolResultEnvelope          // Signal tool-result phase to FastAPI
            };

            _logger.LogDebug("[ChatService] Sending tool result back to AI for final response. Tool={Tool}", toolName);

            var finalAiResponse = await _aiService.SendToolResultAsync(continuationRequest);

            // If the AI itself failed after tool exec, provide a safe fallback
            if (string.IsNullOrWhiteSpace(finalAiResponse.Response))
            {
                return $"The '{toolName}' action was completed successfully.";
            }

            return finalAiResponse.Response;
        }

        // ── Parameter Extraction ─────────────────────────────────────────────

        /// <summary>
        /// Extracts the <c>parameters</c> key from <c>metadata</c>.
        /// FastAPI structure: <c>{ "parameters": { ... } }</c>
        /// Falls back to an empty object if metadata is absent or malformed.
        /// </summary>
        private static object ExtractToolParameters(JsonElement? metadata, string toolName)
        {
            if (metadata == null || metadata.Value.ValueKind == JsonValueKind.Null)
            {
                // No metadata at all — return empty params, tool will validate
                return new Dictionary<string, object>();
            }

            if (metadata.Value.ValueKind != JsonValueKind.Object)
                throw new ArgumentException(
                    $"Expected 'metadata' to be a JSON object for tool '{toolName}', " +
                    $"got {metadata.Value.ValueKind}.");

            // Try to get metadata["parameters"]
            if (metadata.Value.TryGetProperty("parameters", out var parametersElement))
            {
                // Deserialize parameters to a Dictionary<string, JsonElement>
                // so tool implementations can read strongly-typed values via JsonElement.
                var dict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
                    parametersElement.GetRawText());

                return (object?)dict ?? new Dictionary<string, object>();
            }

            // metadata exists but has no "parameters" key — return whole metadata as params
            // This allows simple tools that embed params directly in metadata.
            var fallback = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
                metadata.Value.GetRawText());

            return (object?)fallback ?? new Dictionary<string, object>();
        }

        // ── Delete Message (Admin) ───────────────────────────────────────────

        public async Task DeleteMessageAsync(Ulid messageId)
        {
            var msg = await _context.ChatMessages.FindAsync(messageId);
            if (msg != null)
            {
                _context.ChatMessages.Remove(msg);
                await _context.SaveChangesAsync();
            }
        }
    }
}
