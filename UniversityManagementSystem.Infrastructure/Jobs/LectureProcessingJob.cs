using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NUlid;
using UniversityManagementSystem.Core.Interfaces;

namespace UniversityManagementSystem.Infrastructure.Jobs
{
    /// <summary>
    /// Hangfire job that runs the full lecture processing pipeline asynchronously.
    /// Enqueued immediately after upload — runs in background without blocking the response.
    /// </summary>
    public class LectureProcessingJob(
        IServiceScopeFactory scopeFactory,
        ILogger<LectureProcessingJob> logger)
    {
        public async Task ProcessAsync(string recordingId)
        {
            if (!Ulid.TryParse(recordingId, out var rid))
            {
                logger.LogError("LectureProcessingJob: invalid recordingId {Id}", recordingId);
                return;
            }

            logger.LogInformation("LectureProcessingJob: starting for {Id}", recordingId);

            using var scope = scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<ILectureIntelligenceService>();

            await service.ProcessRecordingAsync(rid);

            logger.LogInformation("LectureProcessingJob: finished for {Id}", recordingId);
        }
    }
}
