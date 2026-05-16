using System;
using NUlid;

namespace UniversityManagementSystem.Core.DTOs
{
    public class NotificationDto
    {
        public Ulid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public bool IsRead { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? ActionUrl { get; set; }
    }

    public class CreateAdminNotificationDto
    {
        public Ulid? UserId { get; set; } // Null for Global
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string? ActionUrl { get; set; }
    }

    public class SendToStudentsDto
    {
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string? ActionUrl { get; set; }
    }
}
