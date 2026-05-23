using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NUlid;
using UniversityManagementSystem.Core.DTOs;
using UniversityManagementSystem.Core.Interfaces;

namespace UniversityManagementSystem.Api.Controllers
{
    /// <summary>
    /// Exposes RAG (Retrieval-Augmented Generation) endpoints for course materials.
    /// Indexing is available to Doctors and Admins; search is available to all authenticated users.
    /// </summary>
    [ApiController]
    [Route("api/rag")]
    [Authorize]
    public class RagController(IRagService ragService) : ControllerBase
    {
        private readonly IRagService _ragService = ragService;

        // ─────────────────────────────────────────────────────────────────────
        // POST /api/rag/index/{materialId}
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Triggers RAG indexing for the specified material.
        /// Downloads content from R2 and delegates chunking + embedding to FastAPI.
        /// </summary>
        [HttpPost("index/{materialId}")]
        [Authorize(Roles = "Doctor,Admin,SuperAdmin")]
        public async Task<IActionResult> IndexMaterial(string materialId)
        {
            if (!Ulid.TryParse(materialId, out var id))
                return BadRequest("Invalid Material ID.");

            try
            {
                await _ragService.IndexMaterialAsync(id);
                return Ok(new { message = "Material indexed successfully.", materialId });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(502, new { error = "Indexing failed.", detail = ex.Message });
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // GET /api/rag/status/{materialId}
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>Returns the current indexing status (chunk count, indexed timestamp) for a material.</summary>
        [HttpGet("status/{materialId}")]
        public async Task<IActionResult> GetIndexingStatus(string materialId)
        {
            if (!Ulid.TryParse(materialId, out var id))
                return BadRequest("Invalid Material ID.");

            var status = await _ragService.GetIndexingStatusAsync(id);
            return Ok(status);
        }

        // ─────────────────────────────────────────────────────────────────────
        // POST /api/rag/search
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Performs a semantic search across indexed material chunks.
        /// Optionally scoped to a specific material or subject offering.
        /// </summary>
        [HttpPost("search")]
        public async Task<IActionResult> Search([FromBody] RagSearchRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Query))
                return BadRequest("Query must not be empty.");

            Ulid? offeringId = null;
            if (!string.IsNullOrWhiteSpace(request.SubjectOfferingId))
            {
                if (!Ulid.TryParse(request.SubjectOfferingId, out var oid))
                    return BadRequest("Invalid SubjectOfferingId.");
                offeringId = oid;
            }

            Ulid? matId = null;
            if (!string.IsNullOrWhiteSpace(request.MaterialId))
            {
                if (!Ulid.TryParse(request.MaterialId, out var mid))
                    return BadRequest("Invalid MaterialId.");
                matId = mid;
            }

            try
            {
                var result = await _ragService.SearchAsync(request.Query, offeringId, matId);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(502, new { error = "Search failed.", detail = ex.Message });
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // GET /api/rag/search/offering/{offeringId}
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Convenience endpoint: semantic search scoped to a subject offering.
        /// Accepts the query via query string parameter.
        /// </summary>
        [HttpGet("search/offering/{offeringId}")]
        public async Task<IActionResult> SearchByOffering(string offeringId, [FromQuery] string query, [FromQuery] int topK = 5)
        {
            if (!Ulid.TryParse(offeringId, out var oid))
                return BadRequest("Invalid Offering ID.");

            if (string.IsNullOrWhiteSpace(query))
                return BadRequest("Query must not be empty.");

            try
            {
                var result = await _ragService.SearchAsync(query, oid, null, topK);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(502, new { error = "Search failed.", detail = ex.Message });
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // DELETE /api/rag/index/{materialId}
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>Deletes all indexed chunks for the specified material.</summary>
        [HttpDelete("index/{materialId}")]
        [Authorize(Roles = "Doctor,Admin,SuperAdmin")]
        public async Task<IActionResult> DeleteIndex(string materialId)
        {
            if (!Ulid.TryParse(materialId, out var id))
                return BadRequest("Invalid Material ID.");

            await _ragService.DeleteChunksAsync(id);
            return NoContent();
        }
    }
}
