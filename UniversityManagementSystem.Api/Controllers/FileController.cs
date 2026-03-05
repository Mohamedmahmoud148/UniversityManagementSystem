using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NUlid;
using System.Security.Claims;
using UniversityManagementSystem.Core.DTOs;
using UniversityManagementSystem.Core.Interfaces;

namespace UniversityManagementSystem.Api.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class FileController(IFileService fileService) : ControllerBase
    {
        private readonly IFileService _fileService = fileService;

        [HttpPost("upload")]
        public async Task<IActionResult> UploadFile([FromBody] UploadFileDto dto)
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Ulid.TryParse(claim, out var userId)) return Unauthorized("Invalid user ID in token.");
            var result = await _fileService.UploadFileAsync(userId, dto);
            return Ok(result);
        }

        [HttpGet]
        public async Task<IActionResult> GetMyFiles()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Ulid.TryParse(claim, out var userId)) return Unauthorized("Invalid user ID in token.");
            var files = await _fileService.GetUserFilesAsync(userId);
            return Ok(files);
        }

        [HttpPut("{id}/rename")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> RenameFile(string id, [FromBody] RenameFileDto dto)
        {
            if (!Ulid.TryParse(id, out var uid)) return BadRequest("Invalid file ID.");
            await _fileService.RenameFileAsync(uid, dto);
            return NoContent();
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteFile(string id)
        {
            if (!Ulid.TryParse(id, out var uid)) return BadRequest("Invalid file ID.");
            await _fileService.DeleteFileAsync(uid);
            return NoContent();
        }
    }
}
