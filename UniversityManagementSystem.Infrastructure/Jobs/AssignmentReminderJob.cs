using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using UniversityManagementSystem.Core.Interfaces;
using UniversityManagementSystem.Infrastructure.Data;

namespace UniversityManagementSystem.Infrastructure.Jobs
{
    /// <summary>
    /// Hangfire job that runs every 30 minutes.
    /// Finds assignments with a deadline within the next 24h or 2h and notifies enrolled students.
    /// Mirrors the ExamReminderJob pattern for consistent student experience.
    /// </summary>
    public class AssignmentReminderJob(
        AppDbContext context,
        INotificationService notificationService,
        ILogger<AssignmentReminderJob> logger) : IAssignmentReminderJob
    {
        private readonly AppDbContext _context = context;
        private readonly INotificationService _notifications = notificationService;
        private readonly ILogger<AssignmentReminderJob> _logger = logger;

        [AutomaticRetry(Attempts = 2)]
        [DisableConcurrentExecution(timeoutInSeconds: 1800)]
        public async Task RunAsync()
        {
            var now   = DateTime.UtcNow;
            var in24h = now.AddHours(24);
            var in2h  = now.AddHours(2);

            // Assignments whose deadline falls within the next 24 hours
            var upcoming = await _context.Assignments
                .AsNoTracking()
                .Include(a => a.SubjectOffering)
                    .ThenInclude(o => o.Subject)
                .Where(a => a.Deadline > now && a.Deadline <= in24h && a.DeletedAt == null)
                .ToListAsync();

            if (upcoming.Count == 0) return;

            _logger.LogInformation("AssignmentReminderJob: {Count} upcoming assignment deadlines to process", upcoming.Count);

            foreach (var assignment in upcoming)
            {
                bool is2hWindow = assignment.Deadline <= in2h;
                string window   = is2hWindow ? "ساعتين" : "يوم";
                string title    = $"تذكير بموعد التسليم — {assignment.Title}";
                string message  = $"الواجب '{assignment.Title}' ({assignment.SubjectOffering?.Subject?.Name}) موعد تسليمه خلال {window}. " +
                                  $"الموعد النهائي: {assignment.Deadline:HH:mm} UTC.";

                // Students enrolled in this offering who haven't submitted yet
                var submittedStudentIds = await _context.AssignmentSubmissions
                    .AsNoTracking()
                    .Where(s => s.AssignmentId == assignment.Id && s.DeletedAt == null)
                    .Select(s => s.StudentId)
                    .ToListAsync();

                var studentUserIds = await _context.Enrollments
                    .AsNoTracking()
                    .Where(e => e.SubjectOfferingId == assignment.SubjectOfferingId
                             && e.IsActive
                             && e.DeletedAt == null
                             && !submittedStudentIds.Contains(e.StudentId))
                    .Select(e => e.Student.SystemUserId)
                    .Distinct()
                    .ToListAsync();

                foreach (var uid in studentUserIds)
                {
                    try
                    {
                        await _notifications.SendNotificationAsync(uid, title, message, actionUrl: "/assignments");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to send assignment reminder to {UserId}", uid);
                    }
                }

                _logger.LogInformation(
                    "Sent assignment deadline reminder for '{Title}' to {Count} students (excluding already-submitted)",
                    assignment.Title, studentUserIds.Count);
            }
        }
    }
}
