using System;
using NUlid;

namespace UniversityManagementSystem.Core.Entities
{
    /// <summary>
    /// Represents a text chunk extracted from a Material, with its embedding stored as a JSON string.
    /// </summary>
    public class MaterialChunk : BaseEntity
    {
        /// <summary>Foreign key to the parent Material.</summary>
        public Ulid MaterialId { get; set; }

        /// <summary>Zero-based position of this chunk within the material.</summary>
        public int ChunkIndex { get; set; }

        /// <summary>The raw text content of this chunk.</summary>
        public string Content { get; set; } = string.Empty;

        /// <summary>
        /// The embedding vector stored as a JSON-serialized float array.
        /// e.g. "[0.123, -0.456, ...]"
        /// Stored as text because pgvector may not be installed.
        /// </summary>
        public string Embedding { get; set; } = string.Empty;

        /// <summary>Approximate token count for this chunk (used for context window budgeting).</summary>
        public int TokenCount { get; set; }

        // Navigation property
        public Material Material { get; set; } = null!;
    }
}
