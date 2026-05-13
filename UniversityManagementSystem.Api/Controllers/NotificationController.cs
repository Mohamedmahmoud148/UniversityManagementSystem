using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NUlid;
using UniversityManagementSystem.Core.DTOs;
using UniversityManagementSystem.Core.Interfaces;

namespace UniversityManagementSystem.Api.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class NotificationController(INotificationService notificationService, IUserContextService userContext) : ControllerBase
    {
        private readonly INotificationService _notificationService = notificationService;
        private readonly IUserContextService _userContext = userContext;

        [HttpGet]
        public async Task<IActionResult> GetNotifications([FromQuery] bool unreadOnly = false)
        {
            var userId = _userContext.GetUserId();
            var notifications = await _notificationService.GetUserNotificationsAsync(userId, unreadOnly);
            return Ok(notifications);
        }

        [HttpPut("{id}/read")]
        public async Task<IActionResult> MarkAsRead(string id)
        {
            if (!Ulid.TryParse(id, out var uid)) return BadRequest("Invalid notification ID.");
            await _notificationService.MarkAsReadAsync(uid);
            return NoContent();
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> SendAdminNotification([FromBody] CreateAdminNotificationDto dto)
        {
            await _notificationService.SendAdminNotificationAsync(dto);
            return Ok(new { message = "Notification sent successfully." });
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteNotification(string id)
        {
            if (!Ulid.TryParse(id, out var uid)) return BadRequest("Invalid notification ID.");
            await _notificationService.DeleteNotificationAsync(uid);
            return NoContent();
        }
    }
}
