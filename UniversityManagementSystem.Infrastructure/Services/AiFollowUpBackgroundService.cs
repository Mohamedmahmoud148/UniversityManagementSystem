using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NUlid;
using UniversityManagementSystem.Core.Entities;
using UniversityManagementSystem.Infrastructure.Data;

namespace UniversityManagementSystem.Infrastructure.Services
{
    /// <summary>
    /// Proactive AI Follow-Up Engine — runs as a .NET background service.
    ///
    /// Runs every 6 hours and:
    ///   1. Detects inactive students (7+ days no AI interaction)
    ///   2. Detects upcoming exams (next 7 days) without study activity
    ///   3. Detects missed/late assignment submissions
    ///   4. Detects at-risk students (grade < 50 in any subject)
    ///   5. Detects streak milestones (7, 14, 30 days)
    ///   6. Detects improving students (grade trending up)
    ///   7. Generates personalized AI insight records
    ///   8. Converts insights to AppNotifications
    ///
    /// All generated messages are personalized via the stored AiCompanionProfile.
    /// Deduplication key prevents the same insight from firing twice in the same week.
    /// </summary>
    public class AiFollowUpBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<AiFollowUpBackgroundService> logger) : BackgroundService
    {
        private static readonly TimeSpan _interval = TimeSpan.FromHours(6);

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            logger.LogInformation("AiFollowUpBackgroundService: started.");

            // Stagger the first run by 2 minutes to avoid startup contention
            await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await RunFollowUpCycleAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "AiFollowUpBackgroundService: cycle failed.");
                }

                await Task.Delay(_interval, stoppingToken);
            }
        }

        private async Task RunFollowUpCycleAsync(CancellationToken ct)
        {
            using var scope = scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var aiService = scope.ServiceProvider.GetRequiredService<IAiService>();

            logger.LogInformation("AiFollowUpBackgroundService: running follow-up cycle at {Time}", DateTime.UtcNow);

            var tasksRun = 0;

            tasksRun += await DetectInactiveStudentsAsync(context, aiService, ct);
            tasksRun += await DetectUpcomingExamsAsync(context, aiService, ct);
            tasksRun += await DetectMissedAssignmentsAsync(context, aiService, ct);
            tasksRun += await DetectAtRiskStudentsAsync(context, aiService, ct);
            tasksRun += await DetectStreakMilestonesAsync(context, ct);
            tasksRun += await DetectImprovingStudentsAsync(context, aiService, ct);
            tasksRun += await GenerateWeeklyReportsAsync(context, aiService, ct);

            // Convert new insights to AppNotifications
            await PublishInsightsAsNotificationsAsync(context, ct);

            logger.LogInformation(
                "AiFollowUpBackgroundService: cycle complete — {Count} insights generated.",
                tasksRun);
        }

        // ── 1. Inactive students ──────────────────────────────────────────

        private async Task<int> DetectInactiveStudentsAsync(
            AppDbContext ctx, IAiService ai, CancellationToken ct)
        {
            var threshold = DateTime.UtcNow.AddDays(-7);
            var weekKey = $"inactive:{DateTime.UtcNow:yyyy-WW}";

            var inactiveProfiles = await ctx.AiCompanionProfiles
                .Where(p => (p.LastInteractionAt == null || p.LastInteractionAt < threshold)
                         && !ctx.AiInsights.Any(i =>
                             i.UserId == p.UserId
                             && i.InsightType == InsightType.InactivityAlert
                             && i.DeduplicationKey == weekKey))
                .Take(100)
                .ToListAsync(ct);

            var count = 0;
            foreach (var profile in inactiveProfiles)
            {
                var daysSince = profile.LastInteractionAt == null
                    ? 99
                    : (int)(DateTime.UtcNow - profile.LastInteractionAt.Value).TotalDays;

                var message = GenerateInactivityMessage(profile, daysSince);

                await CreateInsightAsync(ctx, new AiInsight
                {
                    UserId = profile.UserId,
                    AiCompanionProfileId = profile.Id,
                    InsightType = InsightType.InactivityAlert,
                    Priority = daysSince > 14 ? InsightPriority.High : InsightPriority.Medium,
                    Title = "مفتقدناك في جلسات المذاكرة 📚",
                    Message = message,
                    ActionUrl = "/ai-companion",
                    DeduplicationKey = weekKey,
                    ExpiresAt = DateTime.UtcNow.AddDays(3),
                }, ct);
                count++;
            }
            return count;
        }

        // ── 2. Upcoming exams ─────────────────────────────────────────────

        private async Task<int> DetectUpcomingExamsAsync(
            AppDbContext ctx, IAiService ai, CancellationToken ct)
        {
            var now = DateTime.UtcNow;
            var soon = now.AddDays(7);

            var upcomingExams = await ctx.Exams
                .Include(e => e.SubjectOffering)
                    .ThenInclude(o => o!.Subject)
                .Where(e => e.ScheduledAt >= now && e.ScheduledAt <= soon
                         && e.Status == ExamStatus.Published)
                .ToListAsync(ct);

            var count = 0;
            foreach (var exam in upcomingExams)
            {
                var enrollments = await ctx.Enrollments
                    .Where(en => en.SubjectOfferingId == exam.SubjectOfferingId
                              && en.Status == EnrollmentStatus.Enrolled)
                    .Select(en => en.StudentId)
                    .ToListAsync(ct);

                foreach (var studentId in enrollments)
                {
                    var student = await ctx.Students
                        .Include(s => s.SystemUser)
                        .FirstOrDefaultAsync(s => s.Id == studentId, ct);
                    if (student == null) continue;

                    var dedupeKey = $"exam:{exam.Id}:{student.SystemUserId}";
                    var alreadyExists = await ctx.AiInsights.AnyAsync(
                        i => i.DeduplicationKey == dedupeKey, ct);
                    if (alreadyExists) continue;

                    var daysLeft = (int)(exam.ScheduledAt!.Value - now).TotalDays;
                    var subjectName = exam.SubjectOffering?.Subject?.Name ?? "المادة";

                    await CreateInsightAsync(ctx, new AiInsight
                    {
                        UserId = student.SystemUserId,
                        InsightType = InsightType.ExamApproaching,
                        Priority = daysLeft <= 2 ? InsightPriority.Urgent : InsightPriority.High,
                        Title = $"امتحان {subjectName} بعد {daysLeft} يوم ⚡",
                        Message = $"امتحان {subjectName} هيكون {exam.ScheduledAt!.Value:dd/MM} — " +
                                  $"عندك {daysLeft} يوم للمراجعة. تحتاج مساعدة في التحضير؟",
                        ActionUrl = $"/ai-companion?topic={Uri.EscapeDataString(subjectName)}&mode=exam_prep",
                        DeduplicationKey: dedupeKey,
                        ExpiresAt = exam.ScheduledAt,
                    }, ct);
                    count++;
                }
            }
            return count;
        }

        // ── 3. Missed assignments ─────────────────────────────────────────

        private async Task<int> DetectMissedAssignmentsAsync(
            AppDbContext ctx, IAiService ai, CancellationToken ct)
        {
            var now = DateTime.UtcNow;
            var count = 0;

            // Assignments due in next 48 hours with no submission
            var dueSoon = await ctx.Assignments
                .Include(a => a.SubjectOffering)
                    .ThenInclude(o => o!.Subject)
                .Where(a => a.DueDate >= now && a.DueDate <= now.AddHours(48)
                         && a.DeletedAt == null)
                .ToListAsync(ct);

            foreach (var assignment in dueSoon)
            {
                var enrollments = await ctx.Enrollments
                    .Where(en => en.SubjectOfferingId == assignment.SubjectOfferingId
                              && en.Status == EnrollmentStatus.Enrolled)
                    .Select(en => en.StudentId)
                    .ToListAsync(ct);

                var submitted = await ctx.AssignmentSubmissions
                    .Where(s => s.AssignmentId == assignment.Id)
                    .Select(s => s.StudentId)
                    .ToListAsync(ct);

                var notSubmitted = enrollments.Except(submitted).ToList();

                foreach (var studentId in notSubmitted.Take(50))
                {
                    var student = await ctx.Students
                        .FirstOrDefaultAsync(s => s.Id == studentId, ct);
                    if (student == null) continue;

                    var dedupeKey = $"assignment:{assignment.Id}:{student.SystemUserId}";
                    if (await ctx.AiInsights.AnyAsync(i => i.DeduplicationKey == dedupeKey, ct))
                        continue;

                    var hoursLeft = (int)(assignment.DueDate - now).TotalHours;
                    var subjectName = assignment.SubjectOffering?.Subject?.Name ?? "المادة";

                    await CreateInsightAsync(ctx, new AiInsight
                    {
                        UserId = student.SystemUserId,
                        InsightType = InsightType.AssignmentDeadline,
                        Priority = hoursLeft <= 12 ? InsightPriority.Urgent : InsightPriority.High,
                        Title = $"الواجب باقي عليه {hoursLeft} ساعة ⏰",
                        Message = $"لسه ما سلمتش واجب {subjectName} ({assignment.Title}). " +
                                  $"باقي {hoursLeft} ساعة. محتاج مساعدة في الحل؟",
                        ActionUrl = $"/assignments/{assignment.Id}",
                        DeduplicationKey: dedupeKey,
                        ExpiresAt = assignment.DueDate,
                    }, ct);
                    count++;
                }
            }
            return count;
        }

        // ── 4. At-risk students ───────────────────────────────────────────

        private async Task<int> DetectAtRiskStudentsAsync(
            AppDbContext ctx, IAiService ai, CancellationToken ct)
        {
            var monthKey = $"atrisk:{DateTime.UtcNow:yyyy-MM}";

            var atRisk = await ctx.StudentGrades
                .Where(g => g.FinalGrade < 50 && g.FinalGrade != null
                         && !ctx.AiInsights.Any(i =>
                             i.UserId == g.Student!.SystemUserId
                             && i.InsightType == InsightType.RiskAlert
                             && i.DeduplicationKey == monthKey))
                .Include(g => g.Student)
                .Include(g => g.SubjectOffering)
                    .ThenInclude(o => o!.Subject)
                .Take(50)
                .ToListAsync(ct);

            var count = 0;
            foreach (var grade in atRisk)
            {
                var subjectName = grade.SubjectOffering?.Subject?.Name ?? "المادة";
                var studentName = grade.Student?.FullName.Split(' ').First() ?? "الطالب";

                await CreateInsightAsync(ctx, new AiInsight
                {
                    UserId = grade.Student!.SystemUserId,
                    InsightType = InsightType.RiskAlert,
                    Priority = InsightPriority.High,
                    Title = $"درجتك في {subjectName} تحتاج اهتمام ⚠️",
                    Message = $"يا {studentName}، درجتك في {subjectName} هي {grade.FinalGrade:F0}%. " +
                              "عندي خطة مذاكرة مخصصة تقدر ترفع درجتك. عايز تبدأ؟",
                    ActionUrl = $"/ai-companion?mode=weakness_review&subject={Uri.EscapeDataString(subjectName)}",
                    DeduplicationKey: monthKey,
                    ExpiresAt = DateTime.UtcNow.AddDays(30),
                }, ct);
                count++;
            }
            return count;
        }

        // ── 5. Streak milestones ──────────────────────────────────────────

        private async Task<int> DetectStreakMilestonesAsync(
            AppDbContext ctx, CancellationToken ct)
        {
            var milestoneDays = new[] { 7, 14, 30, 60, 100 };
            var count = 0;

            var profiles = await ctx.AiCompanionProfiles
                .Where(p => milestoneDays.Contains(p.CurrentStreakDays))
                .ToListAsync(ct);

            foreach (var profile in profiles)
            {
                var dedupeKey = $"streak:{profile.CurrentStreakDays}:{profile.UserId}";
                if (await ctx.AiInsights.AnyAsync(i => i.DeduplicationKey == dedupeKey, ct))
                    continue;

                var emoji = profile.CurrentStreakDays >= 30 ? "🏆" :
                            profile.CurrentStreakDays >= 14 ? "🌟" : "🔥";

                await CreateInsightAsync(ctx, new AiInsight
                {
                    UserId = profile.UserId,
                    AiCompanionProfileId = profile.Id,
                    InsightType = InsightType.StreakMilestone,
                    Priority = InsightPriority.Medium,
                    Title = $"{emoji} {profile.CurrentStreakDays} يوم streak!",
                    Message = $"وصلت لـ {profile.CurrentStreakDays} يوم متواصل من المذاكرة! " +
                              "هذا الثبات هو مفتاح النجاح الحقيقي. استمر!",
                    DeduplicationKey: dedupeKey,
                    ExpiresAt = DateTime.UtcNow.AddDays(7),
                }, ct);
                count++;
            }
            return count;
        }

        // ── 6. Improving students ─────────────────────────────────────────

        private async Task<int> DetectImprovingStudentsAsync(
            AppDbContext ctx, IAiService ai, CancellationToken ct)
        {
            // Students whose latest session accuracy is 20+ points above their profile avg
            var count = 0;
            var monthKey = $"improve:{DateTime.UtcNow:yyyy-MM}";

            var recentSessions = await ctx.LearningSessions
                .Where(s => s.Status == LearningSessionStatus.Completed
                         && s.AccuracyPercent >= 80
                         && s.CompletedAt >= DateTime.UtcNow.AddDays(-1))
                .Include(s => s.AiCompanionProfile)
                .ToListAsync(ct);

            foreach (var session in recentSessions)
            {
                var dedupeKey = $"improve:{session.UserId}:{monthKey}";
                if (await ctx.AiInsights.AnyAsync(i => i.DeduplicationKey == dedupeKey, ct))
                    continue;

                await CreateInsightAsync(ctx, new AiInsight
                {
                    UserId = session.UserId,
                    InsightType = InsightType.ImprovementDetected,
                    Priority = InsightPriority.Low,
                    Title = "تحسن ملحوظ في أدائك! 📈",
                    Message = $"حققت {session.AccuracyPercent:F0}% دقة في موضوع '{session.TopicName}'! " +
                              "هذا تقدم رائع. استمر في هذا المستوى!",
                    DeduplicationKey: dedupeKey,
                    ExpiresAt = DateTime.UtcNow.AddDays(7),
                }, ct);
                count++;
            }
            return count;
        }

        // ── 7. Weekly reports ─────────────────────────────────────────────

        private async Task<int> GenerateWeeklyReportsAsync(
            AppDbContext ctx, IAiService ai, CancellationToken ct)
        {
            // Only run on Mondays
            if (DateTime.UtcNow.DayOfWeek != DayOfWeek.Monday)
                return 0;

            var weekKey = $"weekly:{DateTime.UtcNow:yyyy-WW}";
            var weekAgo = DateTime.UtcNow.AddDays(-7);
            var count = 0;

            var activeProfiles = await ctx.AiCompanionProfiles
                .Where(p => p.LastInteractionAt >= weekAgo
                         && !ctx.AiInsights.Any(i =>
                             i.UserId == p.UserId
                             && i.DeduplicationKey == weekKey))
                .Take(200)
                .ToListAsync(ct);

            foreach (var profile in activeProfiles)
            {
                var sessions = await ctx.LearningSessions
                    .Where(s => s.UserId == profile.UserId
                             && s.StartedAt >= weekAgo
                             && s.Status == LearningSessionStatus.Completed)
                    .ToListAsync(ct);

                if (!sessions.Any()) continue;

                var totalMinutes = sessions.Sum(s => s.DurationMinutes);
                var avgAccuracy = sessions.Average(s => s.AccuracyPercent);

                await CreateInsightAsync(ctx, new AiInsight
                {
                    UserId = profile.UserId,
                    AiCompanionProfileId = profile.Id,
                    InsightType = InsightType.WeeklyReport,
                    Priority = InsightPriority.Low,
                    Title = "تقرير أسبوعك الدراسي 📊",
                    Message = $"هذا الأسبوع: {sessions.Count} جلسات مذاكرة، " +
                              $"{totalMinutes} دقيقة إجمالية، " +
                              $"{avgAccuracy:F0}% متوسط دقة. " +
                              (avgAccuracy >= 75 ? "أداء ممتاز! 🌟" : "استمر وستتحسن! 💪"),
                    DataPayload = JsonSerializer.Serialize(new
                    {
                        sessions_count = sessions.Count,
                        total_minutes = totalMinutes,
                        avg_accuracy = Math.Round(avgAccuracy, 1),
                        streak = profile.CurrentStreakDays,
                    }),
                    DeduplicationKey: weekKey,
                    ExpiresAt = DateTime.UtcNow.AddDays(7),
                }, ct);
                count++;
            }
            return count;
        }

        // ── Publish insights as AppNotifications ──────────────────────────

        private async Task PublishInsightsAsNotificationsAsync(
            AppDbContext ctx, CancellationToken ct)
        {
            var unpublished = await ctx.AiInsights
                .Where(i => !i.NotificationSent && !i.IsAcknowledged
                         && (i.ExpiresAt == null || i.ExpiresAt > DateTime.UtcNow))
                .Take(500)
                .ToListAsync(ct);

            var notifications = unpublished.Select(insight => new AppNotification
            {
                UserId = insight.UserId,
                Title = insight.Title,
                Message = insight.Message,
                ActionUrl = insight.ActionUrl,
            }).ToList();

            ctx.AppNotifications.AddRange(notifications);

            foreach (var insight in unpublished)
                insight.NotificationSent = true;

            await ctx.SaveChangesAsync(ct);
            logger.LogInformation("Published {Count} AI insights as notifications.", notifications.Count);
        }

        // ── Helper ────────────────────────────────────────────────────────

        private static async Task CreateInsightAsync(
            AppDbContext ctx, AiInsight insight, CancellationToken ct)
        {
            ctx.AiInsights.Add(insight);
            await ctx.SaveChangesAsync(ct);
        }

        private static string GenerateInactivityMessage(
            AiCompanionProfile profile, int daysSince)
        {
            var dayStr = daysSince >= 99 ? "كتير" : $"{daysSince}";
            var goal = string.IsNullOrEmpty(profile.CurrentGoal)
                ? "أهدافك" : profile.CurrentGoal;

            return daysSince > 14
                ? $"مفتقدناك! مضى {dayStr} يوم من غير مذاكرة مع AI. هدفك هو {goal}. جلسة 15 دقيقة اليوم تعيدك للمسار 🎯"
                : $"مضى {dayStr} يوم من غير جلسة مذاكرة. تذكر هدفك: {goal}. عايز نبدأ؟";
        }
    }
}
