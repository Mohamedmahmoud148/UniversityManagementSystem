using System.Threading.Tasks;

namespace UniversityManagementSystem.Core.Interfaces
{
    /// <summary>Hangfire job interface for batch-indexing unindexed course materials.</summary>
    public interface IRagIndexingJob
    {
        /// <summary>
        /// Finds all Materials that have no associated MaterialChunks and indexes them
        /// by calling <see cref="IRagService.IndexMaterialAsync"/> for each one.
        /// </summary>
        Task IndexAllUnindexedMaterialsAsync();
    }
}
