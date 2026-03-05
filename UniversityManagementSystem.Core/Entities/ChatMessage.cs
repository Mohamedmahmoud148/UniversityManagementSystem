using System;
using NUlid;

namespace UniversityManagementSystem.Core.Entities
{
    public class ChatMessage : BaseEntity
    {
        public Ulid ConversationId { get; set; }
        public string Content { get; set; } = string.Empty;
        public bool IsUserMessage { get; set; }
        public string? Intent { get; set; }

        // Navigation Properties
        public Conversation Conversation { get; set; } = null!;
    }
}
