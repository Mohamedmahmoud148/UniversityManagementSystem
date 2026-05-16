using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using UniversityManagementSystem.Core.Interfaces;
using UniversityManagementSystem.Infrastructure.Data;

namespace UniversityManagementSystem.Infrastructure.Jobs
{
    /// <summary>
    /// Daily Hangfire job: detects at-risk students (low GPA or failing subjects)
    /// and sends them a personalized in-app + real-time notification.
    /// </summary>
    public class AcademicRiskJob(
        AppDbContext context,
        INotificationService notificationService,
        ILogger<AcademicRiskJob> logger) : IAcademicRiskJob
    {
        private readonly AppDbContext _context = context;
        private readonly INotificationService _notifications = notificationService;
        private readonly ILogger<AcademicRiskJob> _logger = logger;

        private const double GpaRiskThreshold = 2.0;

        [AutomaticRetry(Attempts = 2)]
        public async Task RunAsync()
        {
            _logger.LogInformation("AcademicRiskJob started");

            // Students whose average finalized GradePoints is below threshold
            var atRiskStudents = await _context.StudentGrades
                .AsNoTracking()
                .Where(g => g.IsFinalized)
                .GroupBy(g => new { g.Student.SystemUserId, StudentName = g.Student.FullName })
                .Select(g => new
                {
                    g.Key.SystemUserId,
                    g.Key.StudentName,
                    AvgGpa = g.Average(x => x.GradePoints),
                    FailedCount = g.Count(x => x.GradePoints < 1.0)
                })
                .Where(x => x.AvgGpa < GpaRiskThreshold)
                .ToListAsync();

            _logger.LogInformation("Found {Count} at-risk students", atRiskStudents.Count);

            foreach (var student in atRiskStudents)
            {
                var failedPart = student.FailedCount > 0
                    ? $" لديك {student.FailedCount} مادة راسب فيها تحتاج إلى إعادة."
                    : "";

                var message =
                    $"معدلك التراكمي الحالي {student.AvgGpa:F2} أقل من الحد المطلوب ({GpaRiskThreshold}).{failedPart} " +
                    "ننصحك بمراجعة مرشدك الأكاديمي.";

                try
                {
                    await _notifications.SendNotificationAsync(
                        student.SystemUserId,
                        "تنبيه أكاديمي — انخفاض المعدل",
                        message,
                        actionUrl: "/my-grades");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to notify student {UserId}", student.SystemUserId);
                }
            }

            _logger.LogInformation("AcademicRiskJob finished — {Count} notifications sent", atRiskStudents.Count);
        }
    }
}
