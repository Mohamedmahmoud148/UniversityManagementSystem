using System;

namespace UniversityManagementSystem.Core.Entities
{
    public class AppNotification : BaseEntity
    {
        public int UserId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public bool IsRead { get; set; } = false;
        public string? ActionUrl { get; set; }

        // Navigation Properties
        public SystemUser User { get; set; } = null!;
    }
}
