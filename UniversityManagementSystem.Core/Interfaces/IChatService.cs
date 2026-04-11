using System.Collections.Generic;
using System.Threading.Tasks;
using UniversityManagementSystem.Core.DTOs;
using NUlid;

namespace UniversityManagementSystem.Core.Interfaces
{
    public interface IChatService
    {
        Task<Ulid> CreateConversationAsync(Ulid userId, string title);

        /// <summary>
        /// Sends a user message through the AI service, enriches academic context,
        /// and persists both the user message and the AI response.
        ///
        /// Flow: Fetch history → Enrich academic context → Call AI → Save response → Return
        ///
        /// Tool execution is handled entirely by the FastAPI AI service internally.
        /// This method does NOT execute any tools.
        /// </summary>
        Task<ChatResponseDto> SendMessageAsync(Ulid userId, SendMessageDto messageDto, string role);

        Task<IEnumerable<ConversationDto>> GetUserConversationsAsync(Ulid userId);
        Task<IEnumerable<ChatResponseDto>> GetConversationMessagesAsync(Ulid conversationId);

        // Admin Override
        Task DeleteMessageAsync(Ulid messageId);
    }
}
