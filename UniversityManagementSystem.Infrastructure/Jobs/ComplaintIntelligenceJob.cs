using System;
using System.Linq;
using System.Threading.Tasks;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NUlid;
using UniversityManagementSystem.Core.DTOs.Ai;
using UniversityManagementSystem.Core.Entities;
using UniversityManagementSystem.Core.Interfaces;
using UniversityManagementSystem.Infrastructure.Data;

namespace UniversityManagementSystem.Infrastructure.Jobs
{
    public class ComplaintIntelligenceJob : IComplaintIntelligenceJob
    {
        private readonly AppDbContext _context;
        private readonly IAiService _aiService;
        private readonly INotificationService _notificationService;
        private readonly ILogger<ComplaintIntelligenceJob> _logger;

        public ComplaintIntelligenceJob(
            AppDbContext context,
            IAiService aiService,
            INotificationService notificationService,
            ILogger<ComplaintIntelligenceJob> logger)
        {
            _context = context;
            _aiService = aiService;
            _notificationService = notificationService;
            _logger = logger;
        }

        public async Task ProcessNewComplaintAsync(Ulid complaintId)
        {
            try
            {
                var complaint = await _context.Complaints.FirstOrDefaultAsync(c => c.Id == complaintId);
                if (complaint == null) return;

                var request = new AiAnalyzeComplaintRequestDto
                {
                    ComplaintId = complaint.Id.ToString(),
                    Message = complaint.Message,
                    TargetType = complaint.TargetType,
                    TargetId = complaint.TargetId
                };

                var aiResponse = await _aiService.AnalyzeComplaintAsync(request);

                var analysis = new ComplaintAnalysis
                {
                    ComplaintId = complaint.Id,
                    SentimentScore = aiResponse.SentimentScore,
                    Category = aiResponse.Category,
                    Severity = aiResponse.Severity,
                    AiSummary = aiResponse.Summary,
                    DuplicateGroupId = string.IsNullOrWhiteSpace(aiResponse.DuplicateGroupId) ? null : aiResponse.DuplicateGroupId,
                    SuggestedAction = aiResponse.RecommendedAction
                };

                _context.ComplaintAnalyses.Add(analysis);

                if (aiResponse.Severity.Equals("critical", StringComparison.OrdinalIgnoreCase))
                    complaint.Priority = "Critical";
                else if (aiResponse.Severity.Equals("high", StringComparison.OrdinalIgnoreCase))
                    complaint.Priority = "High";

                if (!string.IsNullOrWhiteSpace(analysis.DuplicateGroupId))
                {
                    var cluster = await _context.ComplaintClusters
                        .FirstOrDefaultAsync(c => c.Id.ToString() == analysis.DuplicateGroupId);

                    if (cluster == null)
                    {
                        if (!Ulid.TryParse(analysis.DuplicateGroupId, out var clusterId))
                        {
                            clusterId = Ulid.NewUlid();
                            _logger.LogWarning(
                                "DuplicateGroupId '{Id}' is not a valid ULID — generated new cluster ID {NewId}",
                                analysis.DuplicateGroupId, clusterId);
                        }

                        cluster = new ComplaintCluster
                        {
                            Id = clusterId,
                            Topic = aiResponse.Category,
                            TargetType = complaint.TargetType,
                            TargetId = complaint.TargetId,
                            ComplaintCount = 1,
                            AiSummary = aiResponse.Summary,
                            LastUpdated = DateTime.UtcNow
                        };
                        _context.ComplaintClusters.Add(cluster);
                    }
                    else
                    {
                        cluster.ComplaintCount++;
                        cluster.LastUpdated = DateTime.UtcNow;
                        cluster.AiSummary = "Updated summary based on latest complaint.";
                    }

                    if (cluster.ComplaintCount >= 3)
                    {
                        _logger.LogWarning(
                            "Cluster {ClusterId} is trending with {Count} complaints",
                            cluster.Id, cluster.ComplaintCount);
                    }
                }

                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process complaint {ComplaintId}", complaintId);
            }
        }

        [DisableConcurrentExecution(timeoutInSeconds: 1800)]
        public async Task GenerateDailyReportAsync()
        {
            _logger.LogInformation("Generating daily complaint intelligence report");
            try
            {
                var since = DateTime.UtcNow.Date;
                await SendComplaintReportNotificationAsync("Daily", since);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Daily complaint report failed");
            }
        }

        [DisableConcurrentExecution(timeoutInSeconds: 1800)]
        public async Task GenerateWeeklyReportAsync()
        {
            _logger.LogInformation("Generating weekly complaint intelligence report");
            try
            {
                var since = DateTime.UtcNow.Date.AddDays(-7);
                await SendComplaintReportNotificationAsync("Weekly", since);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Weekly complaint report failed");
            }
        }

        [DisableConcurrentExecution(timeoutInSeconds: 1800)]
        public async Task GenerateMonthlyReportAsync()
        {
            _logger.LogInformation("Generating monthly complaint intelligence report");
            try
            {
                var since = DateTime.UtcNow.Date.AddDays(-30);
                await SendComplaintReportNotificationAsync("Monthly", since);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Monthly complaint report failed");
            }
        }

        private async Task SendComplaintReportNotificationAsync(string period, DateTime since)
        {
            var total = await _context.Complaints
                .Where(c => c.CreatedAt >= since)
                .CountAsync();

            var critical = await _context.Complaints
                .Where(c => c.CreatedAt >= since && c.Priority == "Critical")
                .CountAsync();

            var pending = await _context.Complaints
                .Where(c => c.CreatedAt >= since && c.Status == "Pending")
                .CountAsync();

            var topCategory = await _context.ComplaintAnalyses
                .Where(a => a.Complaint.CreatedAt >= since && a.Category != null)
                .GroupBy(a => a.Category)
                .OrderByDescending(g => g.Count())
                .Select(g => g.Key)
                .FirstOrDefaultAsync() ?? "N/A";

            var summary = $"{period} Report: {total} complaints received, {critical} critical, {pending} pending. Top category: {topCategory}.";

            var admins = await _context.SystemUsers
                .Where(u => u.Role == UserRole.Admin || u.Role == UserRole.SuperAdmin)
                .Select(u => u.Id)
                .ToListAsync();

            foreach (var adminId in admins)
            {
                await _notificationService.SendNotificationAsync(
                    adminId,
                    $"{period} Complaint Report",
                    summary);
            }

            _logger.LogInformation("{Period} complaint report sent to {Count} admins: {Summary}",
                period, admins.Count, summary);
        }
    }
}
