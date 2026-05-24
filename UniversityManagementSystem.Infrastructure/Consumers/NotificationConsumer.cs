using MassTransit;
using Microsoft.Extensions.Logging;
using UniversityManagementSystem.Core.Events;
using UniversityManagementSystem.Core.Interfaces;

namespace UniversityManagementSystem.Infrastructure.Consumers
{
    public class NotificationConsumer(IRealtimeNotifier realtime, ILogger<NotificationConsumer> logger)
        : IConsumer<NotificationCreatedEvent>
    {
        public async Task Consume(ConsumeContext<NotificationCreatedEvent> context)
        {
            var evt = context.Message;
            logger.LogInformation("[NotificationConsumer] Delivering notification to user {UserId}: {Title}", evt.UserId, evt.Title);

            try
            {
                await realtime.PushToUserAsync(evt.UserId.ToString(), evt.Title, evt.Message, evt.ActionUrl);
                logger.LogInformation("[NotificationConsumer] SignalR delivery succeeded for user {UserId}", evt.UserId);
            }
            catch (Exception ex)
            {
                // SignalR failure is non-fatal — notification already persisted in DB
                logger.LogWarning(ex, "[NotificationConsumer] SignalR delivery failed for user {UserId}. User will see notification on next poll.", evt.UserId);
            }
        }
    }
}
