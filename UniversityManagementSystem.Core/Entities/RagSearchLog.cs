using System;
using NUlid;

namespace UniversityManagementSystem.Core.Entities
{
    /// <summary>
    /// Tracks RAG semantic search queries for analytics and audit purposes.
    /// Not soft-deleted — logs are retained permanently.
    /// </summary>
    public class RagSearchLog : BaseEntity
    {
        /// <summary>The student who issued the query.</summary>
        public Ulid StudentId { get; set; }

        /// <summary>The natural-language query text.</summary>
        public string Query { get; set; } = string.Empty;

        /// <summary>JSON array of MaterialChunk IDs that were returned as top-K results.</summary>
        public string RetrievedChunkIds { get; set; } = "[]";

        /// <summary>Optional short summary of the AI response generated from the retrieved chunks.</summary>
        public string? ResponseSummary { get; set; }
    }
}
