using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using UniversityManagementSystem.Core.Interfaces;

namespace UniversityManagementSystem.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MaterialsController(IMaterialService materialService) : ControllerBase
    {
        [HttpPost("upload")]
        [Authorize(Roles = "Doctor")]
        // Consuming multipart/form-data with FromForm
        public async Task<IActionResult> UploadMaterial([FromForm] Core.DTOs.UploadMaterialDto dto)
        {
            var profileClaim = User.Claims.FirstOrDefault(c => c.Type == "ProfileId");
            if (profileClaim == null) return Unauthorized("ProfileId claim not found.");
            var doctorId = int.Parse(profileClaim.Value);

            var material = await materialService.UploadMaterialAsync(dto.OfferingId, doctorId, dto.File);
            return CreatedAtAction(nameof(DownloadMaterial), new { id = material.Id }, material);
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Doctor")]
        public async Task<IActionResult> DeleteMaterial(int id)
        {
            var profileClaim = User.Claims.FirstOrDefault(c => c.Type == "ProfileId");
            if (profileClaim == null) return Unauthorized("ProfileId claim not found.");
            var doctorId = int.Parse(profileClaim.Value);

            await materialService.DeleteMaterialAsync(id, doctorId);
            return NoContent();
        }

        [HttpGet("by-offering/{offeringId}")]
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> GetMaterialsByOffering(int offeringId, [FromQuery] int page = 1, [FromQuery] int pageSize = 10, [FromQuery] string? search = null)
        {
            var profileClaim = User.Claims.FirstOrDefault(c => c.Type == "ProfileId");
            if (profileClaim == null) return Unauthorized("ProfileId claim not found.");
            var studentId = int.Parse(profileClaim.Value);

            var result = await materialService.GetMaterialsByOfferingAsync(offeringId, studentId, page, pageSize, search);
            return Ok(result);
        }

        [HttpGet("download/{id}")]
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> DownloadMaterial(int id)
        {
            var profileClaim = User.Claims.FirstOrDefault(c => c.Type == "ProfileId");
            if (profileClaim == null) return Unauthorized("ProfileId claim not found.");
            var studentId = int.Parse(profileClaim.Value);

            var (filePath, contentType, fileName, lastModified) = await materialService.GetMaterialFileInfoAsync(id, studentId);
            
            // PhysicalFileResult supports Range Processing out of the box with enableRangeProcessing: true
            return PhysicalFile(filePath, contentType, fileName, enableRangeProcessing: true);
        }
    }
}
