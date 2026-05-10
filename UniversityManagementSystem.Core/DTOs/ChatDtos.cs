using System;
using System.Collections.Generic;
using NUlid;

namespace UniversityManagementSystem.Core.DTOs
{
    public class CreateConversationDto
    {
        public string Title { get; set; } = "New Chat";
    }

    public class UpdateConversationTitleDto
    {
        public string Title { get; set; } = string.Empty;
    }

    public class SendMessageDto
    {
        public Ulid ConversationId { get; set; }
        public string? Content { get; set; }
        public string? Message { get; set; }
        
        [System.Text.Json.Serialization.JsonIgnore]
        public string ActualMessage => !string.IsNullOrWhiteSpace(Message) ? Message : (Content ?? string.Empty);
    }

    public class ChatResponseDto
    {
        public Ulid Id { get; set; }
        public string Content { get; set; } = string.Empty;
        public string Sender { get; set; } = string.Empty;
        public bool IsFallback { get; set; }
        public DateTime SentAt { get; set; }
        public List<string> Suggestions { get; set; } = [];
        /// <summary>
        /// Set ONLY on the first message of a conversation — the auto-generated title.
        /// Null on all subsequent messages. Frontend should update sidebar title when non-null.
        /// </summary>
        public string? ConversationTitle { get; set; }
    }

    public class PaginatedChatResponseDto
    {
        public int TotalCount { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public IEnumerable<ChatResponseDto> Items { get; set; } = [];
    }

    public class ConversationDto
    {
        public Ulid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public DateTime LastMessageAt { get; set; }
    }
}
