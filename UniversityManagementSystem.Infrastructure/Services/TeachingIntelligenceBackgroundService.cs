using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using UniversityManagementSystem.Core.Entities;
using UniversityManagementSystem.Core.Interfaces;
using UniversityManagementSystem.Infrastructure.Data;

namespace UniversityManagementSystem.Infrastructure.Services
{
    /// <summary>
    /// Hourly background job that refreshes StudentIntelligenceSnapshot
    /// for all active subject offerings.
    ///
    /// Strategy:
    ///   - On startup: immediate first run after 3 minutes
    ///   - Then: every hour
    ///   - Only processes offerings that have had any activity since last snapshot
    ///   - Generates teaching alerts for doctors when risk escalations detected
    ///
    /// Runs independently from AiFollowUpBackgroundService to avoid
    /// overlapping long-running DB operations.
    /// </summary>
    public class TeachingIntelligenceBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<TeachingIntelligenceBackgroundService> logger) : BackgroundService
    {
        private static readonly TimeSpan _interval = TimeSpan.FromHours(1);

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            logger.LogInformation("TeachingIntelligenceBackgroundService: started.");
            await Task.Delay(TimeSpan.FromMinutes(3), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await RunSnapshotCycleAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "TeachingIntelligenceBackgroundService: cycle failed.");
                }
                await Task.Delay(_interval, stoppingToken);
            }
        }

        private async Task RunSnapshotCycleAsync(CancellationToken ct)
        {
            using var scope = scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var service = scope.ServiceProvider.GetRequiredService<ITeachingIntelligenceService>();

            logger.LogInformation(
                "TeachingIntelligenceBackgroundService: starting snapshot cycle at {Time}",
                DateTime.UtcNow);

            // Get all active offerings that need a snapshot refresh
            var activeOfferingIds = await context.SubjectOfferings
                .Where(o => o.DeletedAt == null)
                .Select(o => o.Id)
                .ToListAsync(ct);

            int refreshed = 0;
            int failed = 0;

            foreach (var offeringId in activeOfferingIds)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    await service.RefreshSnapshotAsync(offeringId);
                    refreshed++;

                    // Generate teaching alerts for risk changes
                    await GenerateTeachingAlertsAsync(context, offeringId, ct);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(
                        "TeachingIntelligenceBackgroundService: failed for offering {Id}: {Err}",
                        offeringId, ex.Message);
                    failed++;
                }

                // Small delay to avoid DB overload
                await Task.Delay(200, ct);
            }

            logger.LogInformation(
                "TeachingIntelligenceBackgroundService: cycle complete — " +
                "{Refreshed} refreshed, {Failed} failed.",
                refreshed, failed);
        }

        private async Task GenerateTeachingAlertsAsync(
            AppDbContext context, Ulid offeringId, CancellationToken ct)
        {
            // Get newly critical students (risk escalated since last alert)
            var criticalStudents = await context.StudentIntelligenceSnapshots
                .Where(s => s.SubjectOfferingId == offeringId
                         && s.RiskLevel == RiskLevel.Critical)
                .Include(s => s.SubjectOffering)
                    .ThenInclude(o => o.Subject)
                .ToListAsync(ct);

            foreach (var snap in criticalStudents.Take(10))
            {
                var dedupeKey = $"teaching_alert:critical:{snap.StudentId}:{offeringId}:{DateTime.UtcNow:yyyy-WW}";
                var exists = await context.AiInsights
                    .AnyAsync(i => i.DeduplicationKey == dedupeKey, ct);
                if (exists) continue;

                var doctorUserId = await context.Doctors
                    .Where(d => d.Id == snap.DoctorId)
                    .Select(d => d.SystemUserId)
                    .FirstOrDefaultAsync(ct);

                if (doctorUserId == default) continue;

                context.AiInsights.Add(new AiInsight
                {
                    UserId = doctorUserId,
                    InsightType = InsightType.ClassPerformanceAlert,
                    Priority = InsightPriority.Urgent,
                    Title = $"🚨 {snap.StudentName} became CRITICAL risk",
                    Message = $"Student {snap.StudentName} in {snap.SubjectName} " +
                              $"has reached CRITICAL risk level (score: {snap.RiskScore:F0}/100). " +
                              $"Reasons: {string.Join(", ", TryParseList(snap.RiskFactors)).ToLower()}. " +
                              $"Recommended: {snap.RecommendedAction}",
                    DataPayload = System.Text.Json.JsonSerializer.Serialize(new
                    {
                        studentId = snap.StudentId.ToString(),
                        studentName = snap.StudentName,
                        offeringId = offeringId.ToString(),
                        subjectName = snap.SubjectName,
                        riskScore = snap.RiskScore,
                    }),
                    ActionUrl = $"/teaching-intelligence/offerings/{offeringId}/students/{snap.StudentId}",
                    DeduplicationKey = dedupeKey,
                    ExpiresAt = DateTime.UtcNow.AddDays(7),
                });
            }

            // Alert for low class attendance
            var offering = await context.SubjectOfferings
                .Include(o => o.Subject)
                .FirstOrDefaultAsync(o => o.Id == offeringId, ct);

            if (offering != null)
            {
                var snapshots = await context.StudentIntelligenceSnapshots
                    .Where(s => s.SubjectOfferingId == offeringId)
                    .ToListAsync(ct);

                if (snapshots.Count >= 5)
                {
                    double avgAtt = snapshots.Average(s => s.AttendancePercent);
                    if (avgAtt < 65)
                    {
                        var attDedupeKey = $"teaching_alert:att:{offeringId}:{DateTime.UtcNow:yyyy-MM}";
                        var attExists = await context.AiInsights
                            .AnyAsync(i => i.DeduplicationKey == attDedupeKey, ct);

                        if (!attExists)
                        {
                            var doctorUserId = await context.Doctors
                                .Where(d => d.Id == offering.DoctorId)
                                .Select(d => d.SystemUserId)
                                .FirstOrDefaultAsync(ct);

                            if (doctorUserId != default)
                            {
                                context.AiInsights.Add(new AiInsight
                                {
                                    UserId = doctorUserId,
                                    InsightType = InsightType.ClassPerformanceAlert,
                                    Priority = avgAtt < 50 ? InsightPriority.Urgent : InsightPriority.High,
                                    Title = $"📉 Low attendance in {offering.Subject?.Name}",
                                    Message = $"Average attendance in {offering.Subject?.Name} dropped to {avgAtt:F0}%. " +
                                              $"{snapshots.Count(s => s.AttendancePercent < 65)} students below threshold.",
                                    DeduplicationKey = attDedupeKey,
                                    ExpiresAt = DateTime.UtcNow.AddDays(14),
                                });
                            }
                        }
                    }

                    // Alert for low assignment completion
                    double avgCompletion = snapshots.Average(s => s.AssignmentCompletionRate);
                    if (avgCompletion < 50)
                    {
                        var compDedupeKey = $"teaching_alert:comp:{offeringId}:{DateTime.UtcNow:yyyy-MM}";
                        var compExists = await context.AiInsights
                            .AnyAsync(i => i.DeduplicationKey == compDedupeKey, ct);

                        if (!compExists)
                        {
                            var missCount = snapshots.Sum(s => s.MissingAssignments);
                            var doctorUserId = await context.Doctors
                                .Where(d => d.Id == offering.DoctorId)
                                .Select(d => d.SystemUserId)
                                .FirstOrDefaultAsync(ct);

                            if (doctorUserId != default)
                            {
                                context.AiInsights.Add(new AiInsight
                                {
                                    UserId = doctorUserId,
                                    InsightType = InsightType.ClassPerformanceAlert,
                                    Priority = InsightPriority.High,
                                    Title = $"📝 Low assignment completion in {offering.Subject?.Name}",
                                    Message = $"Only {avgCompletion:F0}% of assignments are submitted in {offering.Subject?.Name}. " +
                                              $"{missCount} total missing submissions.",
                                    DeduplicationKey = compDedupeKey,
                                    ExpiresAt = DateTime.UtcNow.AddDays(7),
                                });
                            }
                        }
                    }
                }
            }

            await context.SaveChangesAsync(ct);
        }

        private static System.Collections.Generic.List<string> TryParseList(string json)
        {
            try { return System.Text.Json.JsonSerializer.Deserialize<System.Collections.Generic.List<string>>(json) ?? []; }
            catch { return []; }
        }
    }
}
