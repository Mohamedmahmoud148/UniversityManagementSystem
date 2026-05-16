using System.Collections.Generic;
using System.Threading.Tasks;
using UniversityManagementSystem.Core.DTOs;
using NUlid;

namespace UniversityManagementSystem.Core.Interfaces
{
    public interface INotificationService
    {
        Task SendNotificationAsync(Ulid userId, string title, string message, string? actionUrl = null);
        Task SendAdminNotificationAsync(CreateAdminNotificationDto dto);
        Task<IEnumerable<NotificationDto>> GetUserNotificationsAsync(Ulid userId, bool unreadOnly = false);
        Task MarkAsReadAsync(Ulid notificationId);
        Task DeleteNotificationAsync(Ulid notificationId);

        /// <summary>
        /// Sends a notification to all students enrolled in a doctor's offerings.
        /// If offeringId is provided, only that offering's students are targeted.
        /// </summary>
        Task<int> SendToOfferingStudentsAsync(Ulid doctorSystemUserId, string title, string message, string? offeringId = null, string? actionUrl = null);
    }
}
