using System;
using System.Linq;
using System.Threading.Tasks;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using UniversityManagementSystem.Core.Interfaces;
using UniversityManagementSystem.Infrastructure.Data;

namespace UniversityManagementSystem.Infrastructure.Jobs
{
    /// <summary>
    /// Daily Hangfire job that finds all Materials with no associated MaterialChunks
    /// and triggers RAG indexing for each one via <see cref="IRagService"/>.
    /// </summary>
    public class RagIndexingJob(
        AppDbContext context,
        IRagService ragService,
        ILogger<RagIndexingJob> logger) : IRagIndexingJob
    {
        private readonly AppDbContext _context = context;
        private readonly IRagService _ragService = ragService;
        private readonly ILogger<RagIndexingJob> _logger = logger;

        /// <inheritdoc/>
        [AutomaticRetry(Attempts = 1)]
        [DisableConcurrentExecution(timeoutInSeconds: 3600)]
        public async Task IndexAllUnindexedMaterialsAsync()
        {
            _logger.LogInformation("RagIndexingJob: scanning for unindexed materials");

            // Materials that have no chunk rows yet
            var unindexedIds = await _context.Materials
                .AsNoTracking()
                .Where(m => !_context.MaterialChunks.Any(c => c.MaterialId == m.Id))
                .Select(m => m.Id)
                .ToListAsync();

            _logger.LogInformation("RagIndexingJob: found {Count} unindexed materials", unindexedIds.Count);

            foreach (var materialId in unindexedIds)
            {
                try
                {
                    await _ragService.IndexMaterialAsync(materialId);
                    _logger.LogInformation("RagIndexingJob: successfully indexed material {MaterialId}", materialId);
                }
                catch (Exception ex)
                {
                    // Log and continue — one failure should not abort the whole batch
                    _logger.LogError(ex, "RagIndexingJob: failed to index material {MaterialId}", materialId);
                }
            }

            _logger.LogInformation("RagIndexingJob: completed");
        }
    }
}
