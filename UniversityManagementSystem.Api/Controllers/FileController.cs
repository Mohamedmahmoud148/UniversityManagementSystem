using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
            var result = await _fileService.UploadFileAsync(userId, dto);
            return Ok(result);
        }

        [HttpGet]
        public async Task<IActionResult> GetMyFiles()
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
            var files = await _fileService.GetUserFilesAsync(userId);
            return Ok(files);
        }

        [HttpPut("{id}/rename")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> RenameFile(int id, [FromBody] RenameFileDto dto)
        {
            await _fileService.RenameFileAsync(id, dto);
            return NoContent();
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteFile(int id)
        {
            await _fileService.DeleteFileAsync(id);
            return NoContent();
        }
    }
}
