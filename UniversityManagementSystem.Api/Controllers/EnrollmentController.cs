using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using UniversityManagementSystem.Core.Interfaces;
using UniversityManagementSystem.Infrastructure.Data;
using NUlid;

namespace UniversityManagementSystem.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class EnrollmentController(
        IEnrollmentUploadService enrollmentUploadService,
        IUserContextService userContext) : ControllerBase
    {
        private readonly IEnrollmentUploadService _enrollmentUploadService = enrollmentUploadService;
        private readonly IUserContextService _userContext = userContext;

        private static readonly string[] AllowedExtensions = [".xlsx", ".csv"];
        private const long MaxSizeBytes = 10 * 1024 * 1024; // 10 MB

        /// <summary>
        /// Upload an Excel (.xlsx) or CSV file to bulk-import students.
        /// Resolves DepartmentCode, BatchCode, and GroupCode to their database IDs.
        /// Returns a summary of created vs skipped students.
        /// </summary>
        [HttpPost("upload")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        [RequestSizeLimit(10_485_760)]
        public async Task<IActionResult> Upload(IFormFile file)
        {
            var adminId = _userContext.GetUserId();

            if (file == null || file.Length == 0)
                return BadRequest("No file provided.");

            var ext = System.IO.Path.GetExtension(file.FileName).ToLower();
            if (!System.Array.Exists(AllowedExtensions, e => e == ext))
                return BadRequest("Only .xlsx and .csv files are supported for enrollment import.");

            if (file.Length > MaxSizeBytes)
                return BadRequest("File exceeds the 10 MB size limit.");

            var result = await _enrollmentUploadService.ProcessExcelAsync(adminId, file);
            return Ok(result);
        }
    }
}
