using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using UniversityManagementSystem.Core.Interfaces;

namespace UniversityManagementSystem.Infrastructure.Services
{
    /// <summary>
    /// Periodically re-enqueues complaints that never got an AI analysis
    /// (e.g. because the AI service was unreachable, or — historically —
    /// because the ComplaintClusters schema patch hadn't been applied yet).
    /// Makes the "AI is analyzing this complaint..." state self-healing
    /// instead of requiring an admin to call /api/complaints/reprocess-pending.
    /// </summary>
    public class ComplaintReprocessBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<ComplaintReprocessBackgroundService> logger) : BackgroundService
    {
        private static readonly TimeSpan _interval = TimeSpan.FromMinutes(15);

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            logger.LogInformation("ComplaintReprocessBackgroundService: started.");
            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = scopeFactory.CreateScope();
                    var complaintService = scope.ServiceProvider.GetRequiredService<IComplaintService>();
                    var requeued = await complaintService.ReprocessUnanalyzedComplaintsAsync();
                    if (requeued > 0)
                        logger.LogInformation(
                            "ComplaintReprocessBackgroundService: requeued {Count} unanalyzed complaints.",
                            requeued);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "ComplaintReprocessBackgroundService: cycle failed.");
                }

                await Task.Delay(_interval, stoppingToken);
            }
        }
    }
}
