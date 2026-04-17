using System;
using NUlid;

namespace UniversityManagementSystem.Core.Entities
{
    public class ChatMessage : BaseEntity
    {
        public Ulid ConversationId { get; set; }
        public string Content { get; set; } = string.Empty;
        public string Sender { get; set; } = string.Empty; // "user" or "assistant"
        public bool IsFallback { get; set; }
        public string? Intent { get; set; }

        // Navigation Properties
        public Conversation Conversation { get; set; } = null!;
    }
}
