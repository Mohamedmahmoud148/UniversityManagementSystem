using System;

namespace UniversityManagementSystem.Core.Entities
{
    public class ChatMessage : BaseEntity
    {
        public int ConversationId { get; set; }
        public string Content { get; set; } = string.Empty;
        public bool IsUserMessage { get; set; } // True if sent by User, False if AI
        public string? Intent { get; set; } // AI Intent if applicable

        // Navigation Properties
        public Conversation Conversation { get; set; } = null!;
    }
}
