using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UniversityManagementSystem.Core.DTOs;
using UniversityManagementSystem.Core.Interfaces;
using UniversityManagementSystem.Infrastructure.Data;
using NUlid;

namespace UniversityManagementSystem.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MaterialsController(
        IMaterialService materialService,
        AppDbContext context,
        IStorageService storageService) : ControllerBase
    {
        [HttpPost("upload")]
        [Authorize(Roles = "Doctor")]
        public async Task<IActionResult> UploadMaterial([FromForm] Core.DTOs.UploadMaterialDto dto)
        {
            var profileClaim = User.Claims.FirstOrDefault(c => c.Type == "ProfileId");
            if (profileClaim == null) return Unauthorized("ProfileId claim not found.");
            var doctorId = Ulid.Parse(profileClaim.Value);

            var material = await materialService.UploadMaterialAsync(dto.OfferingId, doctorId, dto.File);
            return CreatedAtAction(nameof(DownloadMaterial), new { id = material.Id.ToString() }, material);
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Doctor")]
        public async Task<IActionResult> DeleteMaterial(string id)
        {
            if (!Ulid.TryParse(id, out var materialId)) return BadRequest("Invalid Material ID.");
            var profileClaim = User.Claims.FirstOrDefault(c => c.Type == "ProfileId");
            if (profileClaim == null) return Unauthorized("ProfileId claim not found.");
            var doctorId = Ulid.Parse(profileClaim.Value);

            await materialService.DeleteMaterialAsync(materialId, doctorId);
            return NoContent();
        }

        [HttpGet("by-offering/{offeringId}")]
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> GetMaterialsByOffering(string offeringId, [FromQuery] int page = 1, [FromQuery] int pageSize = 10, [FromQuery] string? search = null)
        {
            if (!Ulid.TryParse(offeringId, out var oId)) return BadRequest("Invalid Offering ID.");
            var profileClaim = User.Claims.FirstOrDefault(c => c.Type == "ProfileId");
            if (profileClaim == null) return Unauthorized("ProfileId claim not found.");
            var studentId = Ulid.Parse(profileClaim.Value);

            var result = await materialService.GetMaterialsByOfferingAsync(oId, studentId, page, pageSize, search);
            return Ok(result);
        }

        /// <summary>
        /// Returns a short-lived pre-signed URL (1 hour) for the material.
        /// The client downloads directly from R2 — nothing streams through the backend.
        /// </summary>
        [HttpGet("download/{id}")]
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> DownloadMaterial(string id)
        {
            if (!Ulid.TryParse(id, out var materialId)) return BadRequest("Invalid Material ID.");
            var profileClaim = User.Claims.FirstOrDefault(c => c.Type == "ProfileId");
            if (profileClaim == null) return Unauthorized("ProfileId claim not found.");
            var studentId = Ulid.Parse(profileClaim.Value);

            var storedUrl = await materialService.GetMaterialUrlAsync(materialId, studentId);

            // Generate a 60-minute signed URL instead of exposing the raw public URL
            var signedUrl = await storageService.GenerateSignedUrlAsync(storedUrl, expiryMinutes: 60);

            return Ok(new { SignedUrl = signedUrl, ExpiresInMinutes = 60 });
        }

        // ── GET /api/materials/{id}/metadata ──────────────────────────────────────
        /// <summary>
        /// Returns lightweight file metadata with a signed URL (valid 60 min).
        /// Never exposes the raw storage key or public URL.
        /// </summary>
        [HttpGet("{id}/metadata")]
        [Authorize]
        public async Task<IActionResult> GetMaterialMetadata(string id)
        {
            if (!Ulid.TryParse(id, out var materialId))
                return BadRequest("Invalid Material ID.");

            var material = await context.Materials
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.Id == materialId);

            if (material is null)
                return NotFound($"Material '{id}' not found.");

            // Prefer StorageKey (new); fall back to StoredFileName (legacy rows)
            var key = !string.IsNullOrWhiteSpace(material.StorageKey)
                ? material.StorageKey
                : material.StoredFileName;

            // Generate a 60-minute signed URL — never expose the raw key/URL
            var signedUrl = await storageService.GenerateSignedUrlAsync(key, expiryMinutes: 60);

            return Ok(new
            {
                MaterialId = material.Id.ToString(),
                FileName = material.FileName,
                ContentType = material.ContentType,
                FileSize = material.FileSize,
                UploadedAt = material.UploadedAt,
                SignedUrl = signedUrl,
                ExpiresInMinutes = 60
            });
        }
    }
}
