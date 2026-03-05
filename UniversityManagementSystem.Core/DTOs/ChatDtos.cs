using System;
using System.Collections.Generic;
using NUlid;

namespace UniversityManagementSystem.Core.DTOs
{
    public class CreateConversationDto
    {
        public string Title { get; set; } = string.Empty;
    }

    public class SendMessageDto
    {
        public Ulid ConversationId { get; set; }
        public string Content { get; set; } = string.Empty;
    }

    public class ChatResponseDto
    {
        public Ulid Id { get; set; }
        public string Content { get; set; } = string.Empty;
        public bool IsUserMessage { get; set; }
        public DateTime SentAt { get; set; }
    }

    public class ConversationDto
    {
        public Ulid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public DateTime LastMessageAt { get; set; }
    }
}
