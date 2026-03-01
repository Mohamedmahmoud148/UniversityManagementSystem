using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using UniversityManagementSystem.Core.DTOs;
using UniversityManagementSystem.Core.Interfaces;

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
            var userId = await _systemUserResolver.ResolveSystemUserIdAsync(User);
            var id = await _chatService.CreateConversationAsync(userId, dto.Title);
            return Ok(new { id });
        }

        [HttpGet("conversations")]
        public async Task<IActionResult> GetConversations()
        {
            var userId = await _systemUserResolver.ResolveSystemUserIdAsync(User);
            var conversations = await _chatService.GetUserConversationsAsync(userId);
            return Ok(conversations);
        }

        [HttpGet("conversations/{id}/messages")]
        public async Task<IActionResult> GetMessages(int id)
        {
            var messages = await _chatService.GetConversationMessagesAsync(id);
            return Ok(messages);
        }

        [HttpPost("messages")]
        public async Task<IActionResult> SendMessage([FromBody] SendMessageDto dto)
        {
            var userId = await _systemUserResolver.ResolveSystemUserIdAsync(User);
            var response = await _chatService.SendMessageAsync(userId, dto);
            return Ok(response);
        }

        [HttpDelete("messages/{id}")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<IActionResult> DeleteMessage(int id)
        {
            await _chatService.DeleteMessageAsync(id);
            return NoContent();
        }
    }
}
