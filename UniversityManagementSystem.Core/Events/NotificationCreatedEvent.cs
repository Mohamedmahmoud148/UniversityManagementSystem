using NUlid;

namespace UniversityManagementSystem.Core.Events
{
    public class NotificationCreatedEvent
    {
        public Ulid UserId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string? ActionUrl { get; set; }
    }
}
