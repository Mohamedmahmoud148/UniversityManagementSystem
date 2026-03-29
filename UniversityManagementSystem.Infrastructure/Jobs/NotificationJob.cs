using System;
using System.Threading.Tasks;
using Hangfire;
using Microsoft.Extensions.Logging;
using NUlid;
using UniversityManagementSystem.Core.Interfaces;

namespace UniversityManagementSystem.Infrastructure.Jobs
{
    public interface INotificationJob
    {
        Task SendNotificationAsync(string userId, string title, string message);
    }

    /// <summary>
    /// Hangfire background job for sending push/in-app notifications.
    /// Enqueue with: _jobClient.Enqueue&lt;INotificationJob&gt;(j => j.SendNotificationAsync(userId, title, msg));
    /// </summary>
    public class NotificationJob : INotificationJob
    {
        private readonly INotificationService _notificationService;
        private readonly ILogger<NotificationJob> _logger;

        public NotificationJob(INotificationService notificationService, ILogger<NotificationJob> logger)
        {
            _notificationService = notificationService;
            _logger = logger;
        }

        [AutomaticRetry(Attempts = 3)]
        public async Task SendNotificationAsync(string userId, string title, string message)
        {
            _logger.LogInformation("Sending notification to user {UserId}: {Title}", userId, title);

            try
            {
                if (!Ulid.TryParse(userId, out var uid))
                {
                    _logger.LogWarning("Invalid userId '{UserId}' in NotificationJob — skipping", userId);
                    return;
                }

                await _notificationService.SendNotificationAsync(uid, title, message);
                _logger.LogInformation("Notification sent to {UserId}", userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send notification to {UserId}", userId);
                throw; // Re-throw so Hangfire retries
            }
        }
    }
}
