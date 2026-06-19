using System.Threading.Tasks;

namespace UniversityManagementSystem.Core.Interfaces
{
    public interface IRealtimeNotifier
    {
        // ── Notification delivery (original — used by NotificationConsumer) ──
        Task PushToUserAsync(string userId, string title, string message, string? actionUrl = null);

        // ── Generic event push (used by AuditService + LectureIntelligenceService) ──
        Task SendToGroupAsync(string group, string eventName, object data);
        Task SendToUserAsync(string userId, string eventName, object data);
    }
}
