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
    [Authorize(Roles = "Admin,SuperAdmin")]
    [ApiController]
    [Route("api/[controller]")]
    public class EnrollmentController(IEnrollmentUploadService enrollmentUploadService) : ControllerBase
    {
        private readonly IEnrollmentUploadService _enrollmentUploadService = enrollmentUploadService;

        private static readonly string[] AllowedExtensions = [".xlsx", ".csv"];
        private const long MaxSizeBytes = 10 * 1024 * 1024; // 10 MB

        /// <summary>
        /// Upload an Excel (.xlsx) or CSV file to bulk-import students.
        /// Resolves DepartmentCode, BatchCode, and GroupCode to their database IDs.
        /// Returns a summary of created vs skipped students.
        /// </summary>
        [HttpPost("upload")]
        [RequestSizeLimit(10_485_760)]
        public async Task<IActionResult> Upload(IFormFile file)
        {
            var adminId = ResolveAdminId();
            if (adminId == null) return Unauthorized("Admin identity not found in token.");

            if (file == null || file.Length == 0)
                return BadRequest("No file provided.");

            var ext = System.IO.Path.GetExtension(file.FileName).ToLower();
            if (!System.Array.Exists(AllowedExtensions, e => e == ext))
                return BadRequest("Only .xlsx and .csv files are supported for enrollment import.");

            if (file.Length > MaxSizeBytes)
                return BadRequest("File exceeds the 10 MB size limit.");

            var result = await _enrollmentUploadService.ProcessExcelAsync(adminId.Value, file);
            return Ok(result);
        }

        private Ulid? ResolveAdminId()
        {
            var nameId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrEmpty(nameId) && Ulid.TryParse(nameId, out var uid))
                return uid;
            return null;
        }
    }
}
