using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UniversityManagementSystem.Core.DTOs;
using UniversityManagementSystem.Core.Entities;
using UniversityManagementSystem.Core.Interfaces;
using Hangfire;
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;
using System;
using Microsoft.AspNetCore.Http;
using NUlid;

namespace UniversityManagementSystem.Api.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class StudentsController(IStudentService service, IAuthService authService, IExcelImportService excelImportService) : ControllerBase
    {
        private readonly IStudentService _studentService = service;
        private readonly IAuthService _authService = authService;
        private readonly IExcelImportService _excelImportService = excelImportService;

        [HttpGet]
        public async Task<ActionResult<IEnumerable<StudentDto>>> GetAll([FromQuery] int page = 1, [FromQuery] int size = 10)
        {
            var list = await _studentService.GetPagedStudentsAsync(page, size);
            return Ok(list.Select(s => new StudentDto
            {
                Id = s.Id,
                Code = s.Code,
                FullName = s.FullName,
                Email = s.Email, // Personal Email
                Phone = s.Phone,
                NationalId = s.SystemUser?.NationalId ?? "N/A", // From SystemUser
                UniversityStudentId = s.UniversityStudentId,
                UniversityEmail = s.SystemUser?.UniversityEmail ?? "N/A", // From SystemUser
                BatchId = s.BatchId,
                IsActive = s.IsActive
            }));
        }

        [HttpGet("by-code/{code}")]
        public async Task<ActionResult<StudentDto>> GetByCode(string code)
        {
            var s = await _studentService.GetStudentByCodeAsync(code);
            if (s == null) return NotFound();

            return Ok(new StudentDto
            {
                Id = s.Id,
                Code = s.Code,
                FullName = s.FullName,
                Email = s.Email,
                Phone = s.Phone,
                NationalId = s.SystemUser?.NationalId ?? "N/A",
                UniversityStudentId = s.UniversityStudentId,
                UniversityEmail = s.SystemUser?.UniversityEmail ?? "N/A",
                BatchId = s.BatchId,
                IsActive = s.IsActive
            });
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<StudentDto>> GetStudent(Ulid id)
        {
            var s = await _studentService.GetStudentByIdAsync(id);
            if (s == null) return NotFound();

            return Ok(new StudentDto
            {
                Id = s.Id,
                Code = s.Code,
                FullName = s.FullName,
                Email = s.Email,
                Phone = s.Phone,
                NationalId = s.SystemUser?.NationalId ?? "N/A",
                UniversityStudentId = s.UniversityStudentId,
                UniversityEmail = s.SystemUser?.UniversityEmail ?? "N/A",
                BatchId = s.BatchId,
                IsActive = s.IsActive
            });
        }

        [HttpGet("by-batch/{batchId}")]
        [Authorize(Roles = "Admin,Doctor,TeachingAssistant")]
        public async Task<ActionResult<IEnumerable<StudentDto>>> GetByBatch(Ulid batchId)
        {
            var list = await _studentService.GetStudentsByBatchIdAsync(batchId);
            return Ok(list.Select(s => new StudentDto
            {
                Id = s.Id,
                Code = s.Code,
                FullName = s.FullName,
                Email = s.Email,
                Phone = s.Phone,
                NationalId = s.SystemUser?.NationalId ?? "N/A",
                UniversityStudentId = s.UniversityStudentId,
                UniversityEmail = s.SystemUser?.UniversityEmail ?? "N/A",
                BatchId = s.BatchId,
                IsActive = s.IsActive
            }));
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<StudentDto>> Create(CreateStudentDto dto)
        {
            // Use AuthService to ensure consistent creation (SystemUser + Student)
            var registerDto = new RegisterStudentDto
            {
                FullName = dto.FullName,
                Phone = dto.Phone,
                NationalId = dto.NationalId,
                BatchId = dto.BatchId
            };

            var creatorId = Ulid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
            var authResponse = await _authService.RegisterStudentAsync(registerDto, creatorId);

            // Fetch the created student to return full DTO
            var student = await _studentService.GetStudentByUniversityEmailAsync(authResponse.UniversityEmail!);
            if (student == null) return BadRequest("Failed to retrieve created student");

            return Ok(new StudentDto
            {
                Id = student.Id,
                Code = student.Code,
                FullName = student.FullName,
                Email = student.Email,
                Phone = student.Phone,
                NationalId = student.SystemUser?.NationalId ?? "N/A",
                UniversityStudentId = student.UniversityStudentId,
                UniversityEmail = student.SystemUser?.UniversityEmail ?? "N/A",
                BatchId = student.BatchId,
                IsActive = student.IsActive
            });
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Update(Ulid id, UpdateStudentDto dto)
        {
            try
            {
                await _studentService.UpdateStudentDetailsAsync(id, dto);
                return NoContent();
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(Ulid id)
        {
            try
            {
                await _studentService.DeleteStudentAsync(id);
                return NoContent();
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost("bulk-upload-direct")]
        [Authorize(Roles = "Admin")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> BulkUploadDirect(IFormFile file, [FromServices] IFileService fileService, [FromServices] IBackgroundJobClient jobClient)
        {
            try
            {
                if (file == null || file.Length == 0) return BadRequest("File is empty");

                var userIdClaim = User.FindFirst("nameid");
                if (userIdClaim == null) return Unauthorized("Invalid token claims");

                if (!Ulid.TryParse(userIdClaim.Value, out var userId)) return Unauthorized("Invalid user ID.");
                using var stream = file.OpenReadStream();
                var status = await fileService.UploadFileStreamAsync(userId, stream, file.FileName, file.ContentType, file.Length);

                jobClient.Enqueue<IBulkUploadJob>(x => x.ProcessStudentDirectUpload(status.Id, userId));
                return Accepted(new { JobId = status.Id, Message = "File accepted for direct processing" });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"BulkUploadDirect Error: {ex.Message}");
                return StatusCode(500, "An unexpected error occurred during direct bulk upload.");
            }
        }

        [HttpPost("bulk-upload-ai")]
        [Authorize(Roles = "Admin")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> BulkUploadAi(IFormFile file, [FromServices] IFileService fileService, [FromServices] IBackgroundJobClient jobClient)
        {
            try
            {
                if (file == null || file.Length == 0) return BadRequest("File is empty");

                var userIdClaim = User.FindFirst("nameid");
                if (userIdClaim == null) return Unauthorized("Invalid token claims");

                if (!Ulid.TryParse(userIdClaim.Value, out var userId)) return Unauthorized("Invalid user ID.");
                using var stream = file.OpenReadStream();
                var status = await fileService.UploadFileStreamAsync(userId, stream, file.FileName, file.ContentType, file.Length);

                jobClient.Enqueue<IBulkUploadJob>(x => x.ProcessStudentAiUpload(status.Id, userId));
                return Accepted(new { JobId = status.Id, Message = "File accepted for AI processing" });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"BulkUploadAi Error: {ex.Message}");
                return StatusCode(500, "An unexpected error occurred during AI bulk upload.");
            }
        }

        // ── POST /api/students/import-excel ──────────────────────────────────
        /// <summary>
        /// Bulk-imports students from an Excel file.
        /// Excel columns: FullName | Email | UniversityStudentId | BatchCode | GroupCode
        /// Invalid/duplicate rows are skipped and reported in the Errors list.
        /// </summary>
        [HttpPost("import-excel")]
        [Authorize(Roles = "Admin")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> ImportFromExcel(IFormFile file)
        {
            // Guard: file must be provided
            if (file == null || file.Length == 0)
                return BadRequest("No file uploaded. Please attach an .xlsx file.");

            // Guard: extension check (service also validates, but fail fast here)
            var ext = System.IO.Path.GetExtension(file.FileName);
            if (!ext.Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
                return BadRequest($"Invalid file type '{ext}'. Only .xlsx files are accepted.");

            var result = await _excelImportService.ImportStudentsFromExcelAsync(file);
            return Ok(result);
        }
    }
}
