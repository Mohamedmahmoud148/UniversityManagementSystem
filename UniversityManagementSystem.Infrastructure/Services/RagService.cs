using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NUlid;
using UniversityManagementSystem.Core.DTOs;
using UniversityManagementSystem.Core.Entities;
using UniversityManagementSystem.Core.Interfaces;
using UniversityManagementSystem.Infrastructure.Data;

namespace UniversityManagementSystem.Infrastructure.Services
{
    /// <summary>
    /// Implements RAG (Retrieval-Augmented Generation) for course materials.
    /// Indexing: downloads file bytes from R2, POSTs to FastAPI /api/rag/index.
    /// Search:   POSTs query to FastAPI /api/rag/search, returns matching chunks.
    /// </summary>
    public class RagService(
        AppDbContext context,
        IStorageService storage,
        IHttpClientFactory httpClientFactory,
        ILogger<RagService> logger) : IRagService
    {
        private readonly AppDbContext _context = context;
        private readonly IStorageService _storage = storage;
        private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
        private readonly ILogger<RagService> _logger = logger;

        // ─────────────────────────────────────────────────────────────────────
        // IndexMaterialAsync
        // ─────────────────────────────────────────────────────────────────────

        /// <inheritdoc/>
        public async Task IndexMaterialAsync(Ulid materialId, CancellationToken ct = default)
        {
            _logger.LogInformation("RAG: starting indexing for material {MaterialId}", materialId);

            var material = await _context.Materials
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.Id == materialId, ct)
                ?? throw new KeyNotFoundException($"Material {materialId} not found.");

            // 1. Get a short-lived signed download URL from R2
            string downloadUrl;
            try
            {
                downloadUrl = await _storage.GenerateSignedUrlAsync(material.StorageKey, expiryMinutes: 15);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "RAG: failed to generate signed URL for {StorageKey}", material.StorageKey);
                throw;
            }

            // 2. Download raw bytes so FastAPI can process the file content
            byte[] fileBytes;
            using (var tempClient = _httpClientFactory.CreateClient())
            {
                tempClient.Timeout = TimeSpan.FromMinutes(5);
                fileBytes = await tempClient.GetByteArrayAsync(downloadUrl, ct);
            }

            // 3. Build multipart/form-data for FastAPI
            //    FastAPI endpoint: POST /api/rag/index
            //    Expected fields: materialId (string), file (binary), metadata (JSON string)
            var metadata = JsonSerializer.Serialize(new
            {
                materialId = materialId.ToString(),
                title = material.Title,
                subjectOfferingId = material.SubjectOfferingId.ToString(),
                contentType = material.ContentType
            });

            using var form = new MultipartFormDataContent();
            form.Add(new StringContent(materialId.ToString()), "materialId");
            form.Add(new StringContent(metadata), "metadata");

            var fileContent = new ByteArrayContent(fileBytes);
            fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(
                string.IsNullOrWhiteSpace(material.ContentType) ? "application/octet-stream" : material.ContentType);
            form.Add(fileContent, "file", material.FileName);

            // 4. Call FastAPI
            var fastApiClient = _httpClientFactory.CreateClient("FastApi");
            HttpResponseMessage response;
            try
            {
                response = await fastApiClient.PostAsync("/api/rag/index", form, ct);
                response.EnsureSuccessStatusCode();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "RAG: FastAPI /api/rag/index call failed for material {MaterialId}", materialId);
                throw;
            }

            // 5. Parse returned chunks and persist them
            var payload = await response.Content.ReadFromJsonAsync<FastApiIndexResponse>(cancellationToken: ct)
                ?? throw new InvalidOperationException("FastAPI returned an empty response for /api/rag/index.");

            // Remove stale chunks first (idempotent re-index)
            var existing = await _context.MaterialChunks
                .Where(c => c.MaterialId == materialId)
                .ToListAsync(ct);
            if (existing.Count > 0)
            {
                _context.MaterialChunks.RemoveRange(existing);
                await _context.SaveChangesAsync(ct);
            }

            var chunks = payload.Chunks.Select((c, idx) => new MaterialChunk
            {
                MaterialId = materialId,
                ChunkIndex = c.ChunkIndex >= 0 ? c.ChunkIndex : idx,
                Content = c.Content,
                Embedding = c.Embedding ?? string.Empty,
                TokenCount = c.TokenCount
            }).ToList();

            _context.MaterialChunks.AddRange(chunks);
            await _context.SaveChangesAsync(ct);

            _logger.LogInformation("RAG: indexed {Count} chunks for material {MaterialId}", chunks.Count, materialId);
        }

        // ─────────────────────────────────────────────────────────────────────
        // SearchAsync
        // ─────────────────────────────────────────────────────────────────────

        /// <inheritdoc/>
        public async Task<RagSearchResponse> SearchAsync(string query, Ulid? offeringId, Ulid? materialId, int topK = 5)
        {
            _logger.LogInformation("RAG: search query='{Query}' offeringId={OfferingId} materialId={MaterialId}",
                query, offeringId, materialId);

            var requestBody = new
            {
                query,
                materialId = materialId?.ToString(),
                subjectOfferingId = offeringId?.ToString(),
                topK
            };

            var fastApiClient = _httpClientFactory.CreateClient("FastApi");
            HttpResponseMessage response;
            try
            {
                response = await fastApiClient.PostAsJsonAsync("/api/rag/search", requestBody);
                response.EnsureSuccessStatusCode();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "RAG: FastAPI /api/rag/search call failed");
                throw;
            }

            var payload = await response.Content.ReadFromJsonAsync<FastApiSearchResponse>()
                ?? throw new InvalidOperationException("FastAPI returned empty response for /api/rag/search.");

            // Resolve chunk details from local DB using the IDs FastAPI returned
            var chunkIdStrings = payload.ChunkIds ?? new List<string>();
            var ulidChunkIds = chunkIdStrings
                .Where(s => Ulid.TryParse(s, out _))
                .Select(s => Ulid.Parse(s))
                .ToList();

            List<MaterialChunk> dbChunks = new();
            if (ulidChunkIds.Count > 0)
            {
                dbChunks = await _context.MaterialChunks
                    .Include(c => c.Material)
                    .Where(c => ulidChunkIds.Contains(c.Id))
                    .AsNoTracking()
                    .ToListAsync();
            }

            // Build ordered ChunkDto list preserving FastAPI ranking
            var scoreMap = payload.Scores ?? new Dictionary<string, double>();
            var chunkDtos = ulidChunkIds
                .Select(id =>
                {
                    var db = dbChunks.FirstOrDefault(c => c.Id == id);
                    if (db == null) return null;
                    scoreMap.TryGetValue(id.ToString(), out var score);
                    return new ChunkDto(
                        ChunkId: db.Id.ToString(),
                        Content: db.Content,
                        ChunkIndex: db.ChunkIndex,
                        MaterialId: db.MaterialId.ToString(),
                        MaterialTitle: db.Material?.Title,
                        Score: score);
                })
                .Where(c => c != null)
                .Select(c => c!)
                .ToList();

            // Assemble context string for the caller (concatenated chunk texts)
            var context = string.Join("\n\n---\n\n", chunkDtos.Select(c => c.Content));

            return new RagSearchResponse(query, chunkDtos, context);
        }

        // ─────────────────────────────────────────────────────────────────────
        // GetIndexingStatusAsync
        // ─────────────────────────────────────────────────────────────────────

        /// <inheritdoc/>
        public async Task<IndexingStatusDto> GetIndexingStatusAsync(Ulid materialId)
        {
            var chunks = await _context.MaterialChunks
                .Where(c => c.MaterialId == materialId)
                .AsNoTracking()
                .Select(c => new { c.CreatedAt })
                .ToListAsync();

            var isIndexed = chunks.Count > 0;
            DateTime? indexedAt = isIndexed ? chunks.Min(c => c.CreatedAt) : null;

            return new IndexingStatusDto(
                MaterialId: materialId.ToString(),
                IsIndexed: isIndexed,
                ChunkCount: chunks.Count,
                IndexedAt: indexedAt);
        }

        // ─────────────────────────────────────────────────────────────────────
        // DeleteChunksAsync
        // ─────────────────────────────────────────────────────────────────────

        /// <inheritdoc/>
        public async Task DeleteChunksAsync(Ulid materialId)
        {
            var chunks = await _context.MaterialChunks
                .Where(c => c.MaterialId == materialId)
                .ToListAsync();

            if (chunks.Count == 0) return;

            _context.MaterialChunks.RemoveRange(chunks);
            await _context.SaveChangesAsync();

            _logger.LogInformation("RAG: deleted {Count} chunks for material {MaterialId}", chunks.Count, materialId);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Private DTOs for FastAPI payloads
        // ─────────────────────────────────────────────────────────────────────

        private sealed class FastApiIndexResponse
        {
            public List<FastApiChunk> Chunks { get; set; } = new();
        }

        private sealed class FastApiChunk
        {
            public int ChunkIndex { get; set; }
            public string Content { get; set; } = string.Empty;
            public string? Embedding { get; set; }
            public int TokenCount { get; set; }
        }

        private sealed class FastApiSearchResponse
        {
            public List<string>? ChunkIds { get; set; }
            public Dictionary<string, double>? Scores { get; set; }
        }
    }
}
