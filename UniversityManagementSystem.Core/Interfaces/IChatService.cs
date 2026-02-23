using System.Collections.Generic;
using System.Threading.Tasks;
using UniversityManagementSystem.Core.DTOs;

namespace UniversityManagementSystem.Core.Interfaces
{
    public interface IChatService
    {
        Task<int> CreateConversationAsync(int userId, string title);
        Task<ChatResponseDto> SendMessageAsync(int userId, SendMessageDto messageDto);
        Task<IEnumerable<ConversationDto>> GetUserConversationsAsync(int userId);
        Task<IEnumerable<ChatResponseDto>> GetConversationMessagesAsync(int conversationId);
        
        // Admin Override
        Task DeleteMessageAsync(int messageId);
    }
}
