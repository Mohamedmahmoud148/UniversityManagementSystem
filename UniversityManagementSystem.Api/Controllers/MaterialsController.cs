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
    public class MaterialsController(IMaterialService materialService, AppDbContext context) : ControllerBase
    {
        [HttpPost("upload")]
        [Authorize(Roles = "Doctor")]
        // Consuming multipart/form-data with FromForm
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

        [HttpGet("download/{id}")]
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> DownloadMaterial(string id)
        {
            if (!Ulid.TryParse(id, out var materialId)) return BadRequest("Invalid Material ID.");
            var profileClaim = User.Claims.FirstOrDefault(c => c.Type == "ProfileId");
            if (profileClaim == null) return Unauthorized("ProfileId claim not found.");
            var studentId = Ulid.Parse(profileClaim.Value);

            var (filePath, contentType, fileName, lastModified) = await materialService.GetMaterialFileInfoAsync(materialId, studentId);

            // PhysicalFileResult supports Range Processing out of the box with enableRangeProcessing: true
            return PhysicalFile(filePath, contentType, fileName, enableRangeProcessing: true);
        }

        // ── GET /api/materials/{id}/metadata ─────────────────────────────────
        /// <summary>
        /// Returns lightweight file metadata for the AI orchestration service to
        /// inspect before retrieving the document.  Requires any valid JWT token.
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

            // Construct a relative URL that matches the MaterialService storage path
            var fileUrl = $"/materials/{material.StoredFileName}";

            var dto = new MaterialMetadataDto(
                MaterialId: material.Id.ToString(),
                FileName: material.FileName,
                FileUrl: fileUrl,
                SubjectOfferingId: material.SubjectOfferingId.ToString()
            );

            return Ok(dto);
        }
    }
}

