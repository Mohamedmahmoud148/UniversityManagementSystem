using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using UniversityManagementSystem.Core.DTOs;
using UniversityManagementSystem.Core.Interfaces;

namespace UniversityManagementSystem.Api.Controllers
{
    [Authorize(Roles = "Student")]
    [ApiController]
    [Route("api/[controller]")]
    public class StudentFilesController(
        IStudentFileService studentFileService,
        IUserContextService userContext) : ControllerBase
    {
        private readonly IStudentFileService _studentFileService = studentFileService;
        private readonly IUserContextService _userContext = userContext;

        private static readonly HashSet<string> AllowedMimeTypes = new(System.StringComparer.OrdinalIgnoreCase)
        {
            "application/pdf",
            "text/plain",
            "image/jpeg", "image/png", "image/webp",
            "application/msword",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            "application/vnd.ms-excel",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        };
        private const long MaxSizeBytes = 30 * 1024 * 1024; // 30 MB

        /// <summary>
        /// Upload a file for AI processing.
        /// Extracted text is stored automatically for PDF/TXT files.
        /// </summary>
        [HttpPost("upload")]
        [RequestSizeLimit(31_457_280)]
        public async Task<IActionResult> Upload(IFormFile file)
        {
            var studentId = _userContext.GetProfileId();

            if (file == null || file.Length == 0)
                return BadRequest("No file provided.");

            if (!AllowedMimeTypes.Contains(file.ContentType))
                return BadRequest($"File type '{file.ContentType}' is not supported.");

            if (file.Length > MaxSizeBytes)
                return BadRequest("File exceeds the 30 MB size limit.");

            var result = await _studentFileService.UploadAsync(studentId, file);
            return Ok(result);
        }

        /// <summary>GET /api/StudentFiles/my — returns all files uploaded by the authenticated student.</summary>
        [HttpGet("my")]
        public async Task<IActionResult> GetMyFiles()
        {
            var studentId = _userContext.GetProfileId();
            var files = await _studentFileService.GetMyFilesAsync(studentId);
            return Ok(files);
        }
    }
}
