using System;
using System.Collections.Generic;

namespace UniversityManagementSystem.Core.DTOs
{
    /// <summary>Request to trigger indexing of a single material.</summary>
    public record IndexMaterialRequest(string MaterialId);

    /// <summary>Request payload for a semantic RAG search.</summary>
    public record RagSearchRequest(
        string Query,
        string? SubjectOfferingId,
        string? MaterialId);

    /// <summary>A single retrieved chunk with its relevance score.</summary>
    public record ChunkDto(
        string ChunkId,
        string Content,
        int ChunkIndex,
        string MaterialId,
        string? MaterialTitle,
        double Score);

    /// <summary>Full response from a RAG search, including the assembled context string.</summary>
    public record RagSearchResponse(
        string Query,
        List<ChunkDto> Chunks,
        string Context);

    /// <summary>Indexing status for a specific material.</summary>
    public record IndexingStatusDto(
        string MaterialId,
        bool IsIndexed,
        int ChunkCount,
        DateTime? IndexedAt);
}
