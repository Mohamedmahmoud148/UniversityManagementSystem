using Microsoft.EntityFrameworkCore;
using UniversityManagementSystem.Core.DTOs;
using UniversityManagementSystem.Core.Entities;
using UniversityManagementSystem.Core.Interfaces;
using UniversityManagementSystem.Infrastructure.Data;
using NUlid;

namespace UniversityManagementSystem.Infrastructure.Services
{
    public class NotificationService(AppDbContext context, IAuditService auditService, IRealtimeNotifier realtimeNotifier) : INotificationService
    {
        private readonly AppDbContext _context = context;
        private readonly IAuditService _auditService = auditService;
        private readonly IRealtimeNotifier _realtime = realtimeNotifier;

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

            // Push real-time via SignalR (fire-and-forget — DB is source of truth)
            try { await _realtime.PushToUserAsync(userId.ToString(), title, message, actionUrl); }
            catch { /* SignalR failure must not break the DB write */ }
        }

        public async Task SendAdminNotificationAsync(CreateAdminNotificationDto dto)
        {
            if (dto.UserId.HasValue)
            {
                await SendNotificationAsync(dto.UserId.Value, dto.Title, dto.Message, dto.ActionUrl);
            }
            else
            {
                var notification = new AppNotification
                {
                    UserId = Ulid.Empty,
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

        public async Task<int> SendToOfferingStudentsAsync(
            Ulid doctorSystemUserId,
            string title,
            string message,
            string? offeringId = null,
            string? actionUrl = null)
        {
            // Resolve doctor profile
            var doctor = await _context.Doctors
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.SystemUserId == doctorSystemUserId && d.DeletedAt == null);

            if (doctor == null) return 0;

            // Build offerings query scoped to this doctor
            var offeringsQ = _context.SubjectOfferings
                .AsNoTracking()
                .Where(o => o.DoctorId == doctor.Id);

            if (!string.IsNullOrWhiteSpace(offeringId) && Ulid.TryParse(offeringId, out var oid))
                offeringsQ = offeringsQ.Where(o => o.Id == oid);

            var targetOfferingIds = await offeringsQ.Select(o => o.Id).ToListAsync();

            if (targetOfferingIds.Count == 0) return 0;

            // Get distinct SystemUserIds of enrolled students
            var studentSystemUserIds = await _context.Enrollments
                .AsNoTracking()
                .Where(e => targetOfferingIds.Contains(e.SubjectOfferingId) && e.IsActive && e.DeletedAt == null)
                .Select(e => e.Student.SystemUserId)
                .Distinct()
                .ToListAsync();

            if (studentSystemUserIds.Count == 0) return 0;

            var now = DateTime.UtcNow;
            var notifications = studentSystemUserIds.Select(uid => new AppNotification
            {
                UserId = uid,
                Title = title,
                Message = message,
                IsRead = false,
                ActionUrl = actionUrl,
                CreatedAt = now
            }).ToList();

            _context.AppNotifications.AddRange(notifications);
            await _context.SaveChangesAsync();

            // Push real-time to each student
            var pushTasks = studentSystemUserIds.Select(uid =>
            {
                try { return _realtime.PushToUserAsync(uid.ToString(), title, message, actionUrl); }
                catch { return Task.CompletedTask; }
            });
            await Task.WhenAll(pushTasks);

            return studentSystemUserIds.Count;
        }
    }
}
