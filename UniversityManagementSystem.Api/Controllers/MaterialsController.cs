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
        IStorageService storageService,
        IUserContextService userContext) : ControllerBase
    {
        private static readonly System.Collections.Generic.HashSet<string> AllowedMimeTypes = new(System.StringComparer.OrdinalIgnoreCase)
        {
            "application/pdf",
            "application/msword",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            "application/vnd.ms-powerpoint",
            "application/vnd.openxmlformats-officedocument.presentationml.presentation",
            "application/vnd.ms-excel",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "image/jpeg",
            "image/png",
            "image/gif",
            "video/mp4",
            "video/webm",
            "text/plain",
            "application/zip"
        };
        private const long MaxFileSizeBytes = 500L * 1024 * 1024; // 500 MB

        [HttpPost("upload")]
        [Authorize(Roles = "Doctor,SuperAdmin")]
        [RequestSizeLimit(524_288_000)] // 500 MB
        public async Task<IActionResult> UploadMaterial([FromForm] Core.DTOs.UploadMaterialDto dto)
        {
            var doctorId = userContext.GetProfileId();

            if (string.IsNullOrWhiteSpace(dto.Title))
                return BadRequest("Title is required.");

            if (dto.File == null || dto.File.Length == 0)
                return BadRequest("No file provided.");

            if (!AllowedMimeTypes.Contains(dto.File.ContentType))
                return BadRequest($"File type '{dto.File.ContentType}' is not allowed.");

            if (dto.File.Length > MaxFileSizeBytes)
                return BadRequest("File exceeds the 500 MB size limit.");

            var material = await materialService.UploadMaterialAsync(dto.OfferingId, doctorId, dto.File, dto.Title, dto.Description);
            return CreatedAtAction(nameof(DownloadMaterial), new { id = material.Id.ToString() }, material);
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Doctor,SuperAdmin")]
        public async Task<IActionResult> DeleteMaterial(string id)
        {
            if (!Ulid.TryParse(id, out var materialId)) return BadRequest("Invalid Material ID.");
            var doctorId = userContext.GetProfileId();

            await materialService.DeleteMaterialAsync(materialId, doctorId);
            return NoContent();
        }

        [HttpGet("by-offering/{offeringId}")]
        [Authorize(Roles = "Student,Doctor,Admin,SuperAdmin")]
        public async Task<IActionResult> GetMaterialsByOffering(string offeringId, [FromQuery] int page = 1, [FromQuery] int pageSize = 10, [FromQuery] string? search = null)
        {
            if (!Ulid.TryParse(offeringId, out var oId)) return BadRequest("Invalid Offering ID.");

            var roleClaim    = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value
                            ?? User.FindFirst("role")?.Value
                            ?? "Student";
            var profileClaim = User.FindFirst("ProfileId");
            if (profileClaim == null) return Unauthorized("ProfileId claim not found.");
            var callerId = Ulid.Parse(profileClaim.Value);

            var result = await materialService.GetMaterialsByOfferingAsync(oId, callerId, roleClaim, page, pageSize, search);
            return Ok(result);
        }

        /// <summary>
        /// Returns a short-lived pre-signed URL (1 hour) for the material.
        /// The client downloads directly from R2 — nothing streams through the backend.
        /// </summary>
        [HttpGet("download/{id}")]
        [Authorize(Roles = "Student,Doctor,Admin,SuperAdmin")]
        public async Task<IActionResult> DownloadMaterial(string id)
        {
            if (!Ulid.TryParse(id, out var materialId)) return BadRequest("Invalid Material ID.");

            var roleClaim    = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value
                            ?? User.FindFirst("role")?.Value
                            ?? "Student";
            var profileClaim = User.FindFirst("ProfileId");
            if (profileClaim == null) return Unauthorized("ProfileId claim not found.");
            var callerId = Ulid.Parse(profileClaim.Value);

            var storedUrl = await materialService.GetMaterialUrlAsync(materialId, callerId, roleClaim);

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
                .Include(m => m.File)
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.Id == materialId);

            if (material is null)
                return NotFound($"Material '{id}' not found.");

            // Prefer StorageKey from UploadedFile, fall back to Material's StorageKey/legacy
            var key = material.File?.StorageKey 
                ?? (!string.IsNullOrWhiteSpace(material.StorageKey) ? material.StorageKey : material.StoredFileName);

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
