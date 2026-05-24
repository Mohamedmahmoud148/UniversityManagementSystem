using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using UniversityManagementSystem.Core.Entities;
using UniversityManagementSystem.Core.Interfaces;
using UniversityManagementSystem.Infrastructure.Data;

namespace UniversityManagementSystem.Infrastructure.Jobs
{
    /// <summary>
    /// Hangfire job that runs every 30 minutes.
    /// Finds Published exams starting within the next 24h or 2h and notifies enrolled students.
    /// </summary>
    public class ExamReminderJob(
        AppDbContext context,
        INotificationService notificationService,
        ILogger<ExamReminderJob> logger) : IExamReminderJob
    {
        private readonly AppDbContext _context = context;
        private readonly INotificationService _notifications = notificationService;
        private readonly ILogger<ExamReminderJob> _logger = logger;

        [AutomaticRetry(Attempts = 2)]
        [DisableConcurrentExecution(timeoutInSeconds: 1800)]
        public async Task RunAsync()
        {
            var now = DateTime.UtcNow;
            var in24h = now.AddHours(24);
            var in2h  = now.AddHours(2);

            // Exams starting in the next 24 hours that are Published
            var upcomingExams = await _context.Set<Exam>()
                .AsNoTracking()
                .Include(e => e.SubjectOffering)
                    .ThenInclude(o => o.Subject)
                .Where(e => e.Status == ExamStatus.Published
                         && e.StartTime > now
                         && e.StartTime <= in24h)
                .ToListAsync();

            if (upcomingExams.Count == 0) return;

            _logger.LogInformation("ExamReminderJob: {Count} upcoming exams to process", upcomingExams.Count);

            foreach (var exam in upcomingExams)
            {
                bool is2hWindow = exam.StartTime <= in2h;
                string window   = is2hWindow ? "ساعتين" : "يوم";
                string title    = $"تذكير بامتحان — {exam.Title}";
                string message  = $"امتحان '{exam.Title}' ({exam.SubjectOffering?.Subject?.Name}) يبدأ خلال {window}. " +
                                  $"الوقت: {exam.StartTime:HH:mm} UTC.";

                // Get enrolled students' SystemUserIds
                var studentUserIds = await _context.Enrollments
                    .AsNoTracking()
                    .Where(e => e.SubjectOfferingId == exam.SubjectOfferingId && e.IsActive && e.DeletedAt == null)
                    .Select(e => e.Student.SystemUserId)
                    .Distinct()
                    .ToListAsync();

                foreach (var uid in studentUserIds)
                {
                    try
                    {
                        await _notifications.SendNotificationAsync(uid, title, message, actionUrl: "/exams");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to send exam reminder to {UserId}", uid);
                    }
                }

                _logger.LogInformation("Sent exam reminder for '{ExamTitle}' to {Count} students", exam.Title, studentUserIds.Count);
            }
        }
    }
}
