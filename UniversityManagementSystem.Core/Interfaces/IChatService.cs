using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using UniversityManagementSystem.Core.DTOs;
using NUlid;

namespace UniversityManagementSystem.Core.Interfaces
{
    public interface IChatService
    {
        Task<Ulid> CreateConversationAsync(Ulid userId, string title);

        /// <summary>
        /// Sends a user message through the full AI loop:
        /// User → AI → (optional) Tool → AI → Response.
        /// </summary>
        /// <param name="userId">Resolved system user ID.</param>
        /// <param name="messageDto">Message payload.</param>
        /// <param name="role">Caller's role (used for tool capability checks).</param>
        /// <param name="caller">ClaimsPrincipal of the caller (passed to IAiTool.ExecuteAsync).</param>
        Task<ChatResponseDto> SendMessageAsync(Ulid userId, SendMessageDto messageDto, string role, ClaimsPrincipal caller);

        Task<IEnumerable<ConversationDto>> GetUserConversationsAsync(Ulid userId);
        Task<IEnumerable<ChatResponseDto>> GetConversationMessagesAsync(Ulid conversationId);

        // Admin Override
        Task DeleteMessageAsync(Ulid messageId);
    }
}
