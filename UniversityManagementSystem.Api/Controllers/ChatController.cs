using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;
using System.Text;
using System.Threading;
using UniversityManagementSystem.Core.DTOs;
using UniversityManagementSystem.Core.Interfaces;
using UniversityManagementSystem.Infrastructure.Services;
using NUlid;

namespace UniversityManagementSystem.Api.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class ChatController(
        IChatService chatService,
        IChatStreamingService streamingService,
        ISystemUserResolver systemUserResolver,
        IAiInputSanitizer sanitizer) : ControllerBase
    {
        private readonly IChatService _chatService = chatService;
        private readonly IChatStreamingService _streamingService = streamingService;
        private readonly ISystemUserResolver _systemUserResolver = systemUserResolver;
        private readonly IAiInputSanitizer _sanitizer = sanitizer;

        [HttpPost("conversations")]
        public async Task<IActionResult> CreateConversation([FromBody] CreateConversationDto dto)
        {
            Ulid userId = await _systemUserResolver.ResolveSystemUserIdAsync(User);
            var id = await _chatService.CreateConversationAsync(userId, dto.Title);
            return Ok(new { id });
        }

        [HttpGet("conversations")]
        public async Task<IActionResult> GetConversations()
        {
            Ulid userId = await _systemUserResolver.ResolveSystemUserIdAsync(User);
            var conversations = await _chatService.GetUserConversationsAsync(userId);
            return Ok(conversations);
        }

        [HttpGet("conversations/{id}/messages")]
        public async Task<IActionResult> GetMessages(string id, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
        {
            if (!Ulid.TryParse(id, out var conversationId)) return BadRequest("Invalid Conversation ID.");
            var messages = await _chatService.GetConversationMessagesAsync(conversationId, page, pageSize);
            return Ok(messages);
        }

        [HttpPost("messages")]
        [EnableRateLimiting("AiPolicy")]
        public async Task<IActionResult> SendMessage([FromBody] SendMessageDto dto)
        {
            var (isSafe, reason) = _sanitizer.Validate(dto.Message ?? string.Empty);
            if (!isSafe)
                return BadRequest(new { message = reason });

            dto.Message = _sanitizer.Sanitize(dto.Message ?? string.Empty);

            Ulid userId = await _systemUserResolver.ResolveSystemUserIdAsync(User);
            var role = User.FindFirstValue("role") ?? User.FindFirstValue(ClaimTypes.Role) ?? "student";
            var profileId = User.FindFirstValue("ProfileId");
            var response = await _chatService.SendMessageAsync(userId, dto, role, profileId);
            return Ok(response);
        }

        // ── POST /api/chat/stream — SSE streaming (ChatGPT-style) ────────────

        [HttpPost("stream")]
        [EnableRateLimiting("AiPolicy")]
        public async Task StreamMessage([FromBody] SendMessageDto dto, CancellationToken ct)
        {
            var (isSafe, reason) = _sanitizer.Validate(dto.Message ?? string.Empty);
            if (!isSafe)
            {
                Response.StatusCode = 400;
                await Response.WriteAsync($"data: {{\"type\":\"error\",\"message\":\"{reason}\"}}\n\n", ct);
                return;
            }

            dto.Message = _sanitizer.Sanitize(dto.Message ?? string.Empty);

            Ulid userId = await _systemUserResolver.ResolveSystemUserIdAsync(User);
            var role      = User.FindFirstValue("role") ?? User.FindFirstValue(ClaimTypes.Role) ?? "student";
            var profileId = User.FindFirstValue("ProfileId");

            Response.ContentType = "text/event-stream";
            Response.Headers["Cache-Control"]      = "no-cache";
            Response.Headers["X-Accel-Buffering"]  = "no";
            Response.Headers["Connection"]         = "keep-alive";

            await foreach (var sseFrame in _streamingService.StreamAsync(userId, dto, role, profileId, ct))
            {
                if (ct.IsCancellationRequested) break;
                await Response.WriteAsync(sseFrame, Encoding.UTF8, ct);
                await Response.Body.FlushAsync(ct);
            }
        }

        [HttpDelete("messages/{id}")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<IActionResult> DeleteMessage(string id)
        {
            if (!Ulid.TryParse(id, out var messageId)) return BadRequest("Invalid Message ID.");
            await _chatService.DeleteMessageAsync(messageId);
            return NoContent();
        }

        // ── DELETE /api/Chat/conversations/{id} ──────────────────────────────
        /// <summary>
        /// Deletes a conversation and all its messages.
        /// Only the owner of the conversation may delete it.
        /// Returns 204 on success, 404 if not found, 403 if not owner.
        /// </summary>
        [HttpDelete("conversations/{id}")]
        public async Task<IActionResult> DeleteConversation(string id)
        {
            if (!Ulid.TryParse(id, out var conversationId))
                return BadRequest("Invalid Conversation ID.");

            Ulid userId = await _systemUserResolver.ResolveSystemUserIdAsync(User);
            var (found, authorized) = await _chatService.DeleteConversationAsync(conversationId, userId);

            if (!found) return NotFound(new { message = "Conversation not found." });
            if (!authorized) return StatusCode(403, new { message = "You do not have permission to delete this conversation." });

            return NoContent(); // 204
        }

        // ── PUT /api/Chat/conversations/{id} ─────────────────────────────────
        /// <summary>
        /// Updates the title of a conversation manually.
        /// Only the owner may rename it.
        /// </summary>
        [HttpPut("conversations/{id}")]
        public async Task<IActionResult> UpdateConversationTitle(string id, [FromBody] UpdateConversationTitleDto dto)
        {
            if (!Ulid.TryParse(id, out var conversationId))
                return BadRequest("Invalid Conversation ID.");

            if (string.IsNullOrWhiteSpace(dto.Title))
                return BadRequest(new { message = "Title cannot be empty." });

            Ulid userId = await _systemUserResolver.ResolveSystemUserIdAsync(User);
            var success = await _chatService.UpdateConversationTitleAsync(conversationId, userId, dto.Title);

            if (!success) return NotFound(new { message = "Conversation not found or access denied." });
            return Ok(new { title = dto.Title });
        }
    }
}
