using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NUlid;
using UniversityManagementSystem.Core.DTOs;
using UniversityManagementSystem.Core.Interfaces;

namespace UniversityManagementSystem.Api.Controllers
{
    [Authorize(Roles = "Student")]
    [ApiController]
    [Route("api/[controller]")]
    public class StudentFilesController(IStudentFileService studentFileService) : ControllerBase
    {
        private readonly IStudentFileService _studentFileService = studentFileService;

        private static readonly HashSet<string> AllowedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
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
            var studentId = ResolveStudentId();
            if (studentId == null) return Unauthorized("Student identity not found in token.");

            if (file == null || file.Length == 0)
                return BadRequest("No file provided.");

            if (!AllowedMimeTypes.Contains(file.ContentType))
                return BadRequest($"File type '{file.ContentType}' is not supported.");

            if (file.Length > MaxSizeBytes)
                return BadRequest("File exceeds the 30 MB size limit.");

            var result = await _studentFileService.UploadAsync(studentId.Value, file);
            return Ok(result);
        }

        /// <summary>GET /api/StudentFiles/my — returns all files uploaded by the authenticated student.</summary>
        [HttpGet("my")]
        public async Task<IActionResult> GetMyFiles()
        {
            var studentId = ResolveStudentId();
            if (studentId == null) return Unauthorized("Student identity not found in token.");

            var files = await _studentFileService.GetMyFilesAsync(studentId.Value);
            return Ok(files);
        }

        // ── Private: resolves student ProfileId from JWT claims ─────────────────
        private Ulid? ResolveStudentId()
        {
            // Try "ProfileId" claim first (set during student login)
            var profileClaim = User.Claims.FirstOrDefault(c => c.Type == "ProfileId")?.Value;
            if (!string.IsNullOrEmpty(profileClaim) && Ulid.TryParse(profileClaim, out var fromProfile))
                return fromProfile;

            // Fallback to NameIdentifier
            var nameId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrEmpty(nameId) && Ulid.TryParse(nameId, out var fromName))
                return fromName;

            return null;
        }
    }
}
