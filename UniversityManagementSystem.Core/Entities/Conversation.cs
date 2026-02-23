using System;
using System.Collections.Generic;

namespace UniversityManagementSystem.Core.Entities
{
    public class Conversation : BaseEntity
    {
        public string Title { get; set; } = string.Empty;
        public int UserId { get; set; }
        public bool IsActive { get; set; } = true;

        // Navigation Properties
        public SystemUser User { get; set; } = null!;
        public ICollection<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
    }
}
