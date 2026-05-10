using System;
using System.Threading.Tasks;
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

                // Call FastAPI AI logic
                var aiResponse = await _aiService.AnalyzeComplaintAsync(request);

                // Save analysis
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

                // Check severity and update priority
                if (aiResponse.Severity.Equals("critical", StringComparison.OrdinalIgnoreCase))
                {
                    complaint.Priority = "Critical";
                }
                else if (aiResponse.Severity.Equals("high", StringComparison.OrdinalIgnoreCase))
                {
                    complaint.Priority = "High";
                }

                // Handle clustering (if assigned a group id)
                if (!string.IsNullOrWhiteSpace(analysis.DuplicateGroupId))
                {
                    var cluster = await _context.ComplaintClusters.FirstOrDefaultAsync(c => c.Id.ToString() == analysis.DuplicateGroupId);
                    if (cluster == null)
                    {
                        // Create new cluster if FastApi assigned a new ID
                        cluster = new ComplaintCluster
                        {
                            Id = Ulid.Parse(analysis.DuplicateGroupId), // Assuming FastApi uses ULID format or we map it
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
                        cluster.AiSummary = "Updated summary based on latest complaint."; // In real scenario, AI might regenerate this
                    }

                    // Alert on trending cluster
                    if (cluster.ComplaintCount >= 3)
                    {
                        // Escalation Notification to Admins
                        // For simplicity, omitting exact admin lookup and assuming a broadcast or specific admin user
                        _logger.LogWarning($"Cluster {cluster.Id} is trending! {cluster.ComplaintCount} complaints.");
                        // _notificationService.SendAdminNotificationAsync(...)
                    }
                }

                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to process complaint {complaintId}");
            }
        }

        public async Task GenerateDailyReportAsync()
        {
            _logger.LogInformation("Generating Daily Complaint Intelligence Report...");
            // Aggregation logic here
            await Task.CompletedTask;
        }

        public async Task GenerateWeeklyReportAsync()
        {
            _logger.LogInformation("Generating Weekly Complaint Intelligence Report...");
            await Task.CompletedTask;
        }

        public async Task GenerateMonthlyReportAsync()
        {
            _logger.LogInformation("Generating Monthly Complaint Intelligence Report...");
            await Task.CompletedTask;
        }
    }
}
