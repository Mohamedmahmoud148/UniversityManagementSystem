using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using UniversityManagementSystem.Core.DTOs;
using UniversityManagementSystem.Core.Interfaces;

namespace UniversityManagementSystem.Api.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class FileController(IFileService fileService, IUserContextService userContext) : ControllerBase
    {
        private readonly IFileService _fileService = fileService;
        private readonly IUserContextService _userContext = userContext;

        // Allowed MIME types — covers documents, images, spreadsheets
        private static readonly System.Collections.Generic.HashSet<string> AllowedMimeTypes = new(System.StringComparer.OrdinalIgnoreCase)
        {
            "application/pdf",
            "image/jpeg", "image/png", "image/gif", "image/webp",
            "application/msword",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            "application/vnd.ms-excel",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "text/plain", "text/csv",
            "application/zip"
        };
        private const long MaxFileSizeBytes = 50 * 1024 * 1024; // 50 MB

        /// <summary>
        /// Upload any file (PDF, image, Word, Excel, text …) via multipart/form-data.
        /// Returns a FileId + 60-minute signed download URL.
        /// </summary>
        [HttpPost("upload")]
        [RequestSizeLimit(52_428_800)]
        public async Task<IActionResult> UploadFile(IFormFile file)
        {
            var userId = _userContext.GetUserId();

            if (file == null || file.Length == 0)
                return BadRequest("No file provided.");

            if (!AllowedMimeTypes.Contains(file.ContentType))
                return BadRequest($"File type '{file.ContentType}' is not allowed. Supported: PDF, images, Word, Excel, text.");

            if (file.Length > MaxFileSizeBytes)
                return BadRequest("File exceeds the 50 MB size limit.");

            var result = await _fileService.UploadFormFileAsync(userId, file);
            return Ok(new { FileId = result.FileId });
        }

        /// <summary>GET /api/File — returns all files uploaded by the authenticated user.</summary>
        [HttpGet]
        public async Task<IActionResult> GetMyFiles()
        {
            var userId = _userContext.GetUserId();
            var files = await _fileService.GetUserFilesAsync(userId);
            return Ok(files);
        }

        [HttpPut("{id}/rename")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<IActionResult> RenameFile(string id, [FromBody] RenameFileDto dto)
        {
            if (!NUlid.Ulid.TryParse(id, out var uid)) return BadRequest("Invalid file ID.");
            await _fileService.RenameFileAsync(uid, dto);
            return NoContent();
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<IActionResult> DeleteFile(string id)
        {
            if (!NUlid.Ulid.TryParse(id, out var uid)) return BadRequest("Invalid file ID.");
            await _fileService.DeleteFileAsync(uid);
            return NoContent();
        }
    }
}
