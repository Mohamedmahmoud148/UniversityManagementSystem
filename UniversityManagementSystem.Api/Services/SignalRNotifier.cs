using Microsoft.AspNetCore.SignalR;
using UniversityManagementSystem.Api.Hubs;
using UniversityManagementSystem.Core.Interfaces;

namespace UniversityManagementSystem.Api.Services
{
    public class SignalRNotifier(IHubContext<NotificationHub> hubContext) : IRealtimeNotifier
    {
        private readonly IHubContext<NotificationHub> _hub = hubContext;

        public async Task PushToUserAsync(string userId, string title, string message, string? actionUrl = null)
        {
            await _hub.Clients.Group(userId).SendAsync("ReceiveNotification", new
            {
                title,
                message,
                actionUrl,
                createdAt = DateTime.UtcNow
            });
        }
    }
}
