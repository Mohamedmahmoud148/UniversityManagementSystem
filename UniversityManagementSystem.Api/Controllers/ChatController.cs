using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using UniversityManagementSystem.Core.DTOs;
using UniversityManagementSystem.Core.Interfaces;
using NUlid;

namespace UniversityManagementSystem.Api.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class ChatController(IChatService chatService, ISystemUserResolver systemUserResolver) : ControllerBase
    {
        private readonly IChatService _chatService = chatService;
        private readonly ISystemUserResolver _systemUserResolver = systemUserResolver;

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
        public async Task<IActionResult> GetMessages(string id)
        {
            if (!Ulid.TryParse(id, out var conversationId)) return BadRequest("Invalid Conversation ID.");
            var messages = await _chatService.GetConversationMessagesAsync(conversationId);
            return Ok(messages);
        }

        [HttpPost("messages")]
        public async Task<IActionResult> SendMessage([FromBody] SendMessageDto dto)
        {
            Ulid userId = await _systemUserResolver.ResolveSystemUserIdAsync(User);
            var role = User.FindFirstValue(ClaimTypes.Role) ?? "student";
            var response = await _chatService.SendMessageAsync(userId, dto, role);
            return Ok(response);
        }

        [HttpDelete("messages/{id}")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<IActionResult> DeleteMessage(string id)
        {
            if (!Ulid.TryParse(id, out var messageId)) return BadRequest("Invalid Message ID.");
            await _chatService.DeleteMessageAsync(messageId);
            return NoContent();
        }
    }
}
