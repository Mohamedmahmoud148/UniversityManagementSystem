using System.Threading;
using System.Threading.Tasks;
using NUlid;
using UniversityManagementSystem.Core.DTOs;

namespace UniversityManagementSystem.Core.Interfaces
{
    /// <summary>
    /// Provides RAG (Retrieval-Augmented Generation) capabilities for course materials.
    /// Delegates embedding and similarity search to the FastAPI AI service.
    /// </summary>
    public interface IRagService
    {
        /// <summary>
        /// Downloads the material content from R2 storage and sends it to the FastAPI
        /// /api/rag/index endpoint to be chunked, embedded, and stored in MaterialChunks.
        /// </summary>
        Task IndexMaterialAsync(Ulid materialId, CancellationToken ct = default);

        /// <summary>
        /// Performs a semantic search by forwarding the query to the FastAPI
        /// /api/rag/search endpoint, then fetches and returns the matching chunks.
        /// </summary>
        Task<RagSearchResponse> SearchAsync(string query, Ulid? offeringId, Ulid? materialId, int topK = 5);

        /// <summary>Returns the current indexing status (chunk count, indexed timestamp) for a material.</summary>
        Task<IndexingStatusDto> GetIndexingStatusAsync(Ulid materialId);

        /// <summary>Removes all MaterialChunk records for the specified material.</summary>
        Task DeleteChunksAsync(Ulid materialId);
    }
}
