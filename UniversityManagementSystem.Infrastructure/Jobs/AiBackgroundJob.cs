using System;
using System.Threading.Tasks;
using Hangfire;
using Microsoft.Extensions.Logging;
using NUlid;

namespace UniversityManagementSystem.Infrastructure.Jobs
{
    public interface IAiBackgroundJob
    {
        Task GenerateExamAsync(string subjectOfferingId, string doctorId, int questionCount);
        Task SummarizeSubjectMaterialAsync(string subjectOfferingId);
    }

    /// <summary>
    /// Hangfire background job for heavy AI operations (exam generation, summarization).
    /// Moves CPU/network-heavy AI calls off the request thread to avoid timeouts.
    ///
    /// Usage:
    ///   _jobClient.Enqueue&lt;IAiBackgroundJob&gt;(j => j.GenerateExamAsync(offeringId, doctorId, 10));
    ///   _jobClient.Enqueue&lt;IAiBackgroundJob&gt;(j => j.SummarizeSubjectMaterialAsync(offeringId));
    /// </summary>
    public class AiBackgroundJob : IAiBackgroundJob
    {
        private readonly ILogger<AiBackgroundJob> _logger;

        public AiBackgroundJob(ILogger<AiBackgroundJob> logger)
        {
            _logger = logger;
        }

        [AutomaticRetry(Attempts = 2)]
        [Queue("ai")]          // Isolated Hangfire queue — deploy dedicated worker if needed
        public async Task GenerateExamAsync(string subjectOfferingId, string doctorId, int questionCount)
        {
            _logger.LogInformation(
                "AI exam generation started — offering: {OfferingId}, doctor: {DoctorId}, questions: {Count}",
                subjectOfferingId, doctorId, questionCount);

            // ── Replace with your actual IAiService call ──────────────────────
            // var result = await _aiService.GenerateExamAsync(Ulid.Parse(subjectOfferingId), questionCount);
            // await _examService.SaveGeneratedExamAsync(result, Ulid.Parse(doctorId));

            await Task.Delay(100); // Placeholder
            _logger.LogInformation("AI exam generation complete for offering {OfferingId}", subjectOfferingId);
        }

        [AutomaticRetry(Attempts = 2)]
        [Queue("ai")]
        public async Task SummarizeSubjectMaterialAsync(string subjectOfferingId)
        {
            _logger.LogInformation("AI summarization started for offering {OfferingId}", subjectOfferingId);

            // ── Replace with your actual IAiService call ──────────────────────
            // await _aiService.SummarizeMaterialsAsync(Ulid.Parse(subjectOfferingId));

            await Task.Delay(100);
            _logger.LogInformation("AI summarization complete for {OfferingId}", subjectOfferingId);
        }
    }
}
