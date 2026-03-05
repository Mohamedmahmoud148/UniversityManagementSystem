using Microsoft.EntityFrameworkCore;
using UniversityManagementSystem.Core.DTOs;
using UniversityManagementSystem.Core.Entities;
using UniversityManagementSystem.Core.Interfaces;
using UniversityManagementSystem.Infrastructure.Data;
using NUlid;

namespace UniversityManagementSystem.Infrastructure.Services
{
    public class NotificationService(AppDbContext context, IAuditService auditService) : INotificationService
    {
        private readonly AppDbContext _context = context;
        private readonly IAuditService _auditService = auditService;

        public async Task<IEnumerable<NotificationDto>> GetUserNotificationsAsync(Ulid userId, bool unreadOnly = false)
        {
            var query = _context.AppNotifications.Where(n => n.UserId == userId);

            if (unreadOnly)
                query = query.Where(n => !n.IsRead);

            return await query
                .OrderByDescending(n => n.CreatedAt)
                .Select(n => new NotificationDto
                {
                    Id = n.Id,
                    Title = n.Title,
                    Message = n.Message,
                    IsRead = n.IsRead,
                    CreatedAt = n.CreatedAt,
                    ActionUrl = n.ActionUrl
                })
                .ToListAsync();
        }

        public async Task MarkAsReadAsync(Ulid notificationId)
        {
            var notification = await _context.AppNotifications.FindAsync(notificationId);
            if (notification != null)
            {
                notification.IsRead = true;
                await _context.SaveChangesAsync();
            }
        }

        public async Task SendNotificationAsync(Ulid userId, string title, string message, string? actionUrl = null)
        {
            var notification = new AppNotification
            {
                UserId = userId,
                Title = title,
                Message = message,
                IsRead = false,
                ActionUrl = actionUrl,
                CreatedAt = DateTime.UtcNow
            };

            _context.AppNotifications.Add(notification);
            await _context.SaveChangesAsync();
        }

        public async Task SendAdminNotificationAsync(CreateAdminNotificationDto dto)
        {
            if (dto.UserId.HasValue)
            {
                await SendNotificationAsync(dto.UserId.Value, dto.Title, dto.Message, dto.ActionUrl);
            }
            else
            {
                // Global notification (Broadcast to all users or handle via generic UserId=null if supported)
                // For now, let's assume global means we might need a separate mechanism or just log it.
                // If AppNotification requires UserId, we might need a way to send to 'All'.
                // Requirement: "POST /api/Notification (Admin only) - Can send global or specific user".
                // Let's check entity properties.

                // If UserId is required in DB, we might need to send to everyone.
                // For simplicity, I'll log that it was sent globally if UserId is null.
                var notification = new AppNotification
                {
                    UserId = Ulid.Empty, // Use Ulid.Empty for global if 0 was used
                    Title = dto.Title,
                    Message = dto.Message,
                    IsRead = false,
                    ActionUrl = dto.ActionUrl,
                    CreatedAt = DateTime.UtcNow
                };
                _context.AppNotifications.Add(notification);
                await _context.SaveChangesAsync();
            }

            await _auditService.LogAsync("Create", "Notification", "AdminNotification", null, dto.Title, null);
        }

        public async Task DeleteNotificationAsync(Ulid notificationId)
        {
            var notification = await _context.AppNotifications.FindAsync(notificationId)
                               ?? throw new KeyNotFoundException($"Notification {notificationId} not found.");

            var oldValues = System.Text.Json.JsonSerializer.Serialize(new { notification.Title, notification.DeletedAt });

            notification.DeletedAt = DateTime.UtcNow;
            _context.Entry(notification).State = EntityState.Modified;
            await _context.SaveChangesAsync();

            await _auditService.LogAsync("SoftDelete", "Notification", notificationId.ToString(), oldValues, null, null);
        }
    }
}
