using Microsoft.AspNetCore.SignalR;
using UniversityManagementSystem.Api.Hubs;
using UniversityManagementSystem.Core.Interfaces;

namespace UniversityManagementSystem.Api.Services
{
    public class SignalRNotifier(
        IHubContext<NotificationHub> notificationHub,
        IHubContext<AuditHub> auditHub) : IRealtimeNotifier
    {
        // ── Notification delivery ─────────────────────────────────────────────
        public Task PushToUserAsync(string userId, string title, string message, string? actionUrl = null)
            => notificationHub.Clients.Group(userId).SendAsync("ReceiveNotification", new
            {
                title,
                message,
                actionUrl,
                createdAt = DateTime.UtcNow
            });

        // ── Generic group push (e.g. audit admins group) ──────────────────────
        public Task SendToGroupAsync(string group, string eventName, object data)
            => auditHub.Clients.Group(group).SendAsync(eventName, data);

        // ── Generic user push (e.g. lecture processing status) ────────────────
        public Task SendToUserAsync(string userId, string eventName, object data)
            => notificationHub.Clients.Group(userId).SendAsync(eventName, data);
    }
}
