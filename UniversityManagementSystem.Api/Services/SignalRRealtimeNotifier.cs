using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using UniversityManagementSystem.Api.Hubs;
using UniversityManagementSystem.Core.Interfaces;

namespace UniversityManagementSystem.Api.Services
{
    public class SignalRRealtimeNotifier(
        IHubContext<AuditHub> auditHub,
        IHubContext<NotificationHub> notificationHub) : IRealtimeNotifier
    {
        public Task SendToGroupAsync(string group, string eventName, object data)
            => auditHub.Clients.Group(group).SendAsync(eventName, data);

        public Task SendToUserAsync(string userId, string eventName, object data)
            => notificationHub.Clients.Group(userId).SendAsync(eventName, data);
    }
}
