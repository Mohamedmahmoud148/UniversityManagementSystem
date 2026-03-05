using System.Collections.Generic;
using System.Threading.Tasks;
using UniversityManagementSystem.Core.DTOs;
using NUlid;

namespace UniversityManagementSystem.Core.Interfaces
{
    public interface IChatService
    {
        Task<Ulid> CreateConversationAsync(Ulid userId, string title);
        Task<ChatResponseDto> SendMessageAsync(Ulid userId, SendMessageDto messageDto, string role);
        Task<IEnumerable<ConversationDto>> GetUserConversationsAsync(Ulid userId);
        Task<IEnumerable<ChatResponseDto>> GetConversationMessagesAsync(Ulid conversationId);

        // Admin Override
        Task DeleteMessageAsync(Ulid messageId);
    }
}
