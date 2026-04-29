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
using Microsoft.EntityFrameworkCore;
using UniversityManagementSystem.Infrastructure.Data;

namespace UniversityManagementSystem.Api.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class StudentsController(
        IStudentService service,
        IAuthService authService,
        IExcelImportService excelImportService,
        IBatchService batchService,
        IGroupService groupService,
        AppDbContext context,
        IDistributedCache cache) : ControllerBase
    {
        private const string StudentListCachePrefix = "students:page:";
        private static readonly DistributedCacheEntryOptions _studentListCacheOpts = new()
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
        };

        private readonly IStudentService _studentService = service;
        private readonly IAuthService _authService = authService;
        private readonly IExcelImportService _excelImportService = excelImportService;
        private readonly IBatchService _batchService = batchService;
        private readonly IGroupService _groupService = groupService;
        private readonly AppDbContext _context = context;
        private readonly IDistributedCache _cache = cache;

        // ── GET /api/Students/search?q= ──────────────────────────────────────
        [HttpGet("search")]
        public async Task<IActionResult> Search([FromQuery] string? q)
        {
            if (string.IsNullOrWhiteSpace(q))
                return BadRequest("Search query 'q' is required.");

            var pattern = $"%{q}%";
            var results = await _context.Students
                .AsNoTracking()
                .Where(s =>
                    EF.Functions.ILike(s.FullName, pattern) ||
                    EF.Functions.ILike(s.Code, pattern) ||
                    EF.Functions.ILike(s.Email, pattern) ||
                    EF.Functions.ILike(s.UniversityStudentId, pattern))
                .OrderBy(s => s.FullName)
                .Take(20)
                .Select(s => new
                {
                    s.Id,
                    s.Code,
                    s.FullName,
                    s.Email,
                    s.UniversityStudentId,
                    s.BatchId,
                    s.IsActive
                })
                .ToListAsync();

            return Ok(results);
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<StudentDto>>> GetAll([FromQuery] int page = 1, [FromQuery] int size = 10)
        {
            var cacheKey = $"{StudentListCachePrefix}{page}:size:{size}";
            var cached = await _cache.GetStringAsync(cacheKey);
            if (!string.IsNullOrEmpty(cached))
                return Content(cached, "application/json");

            var list = await _studentService.GetPagedStudentsAsync(page, size);
            var dtos = list.Select(s => new StudentDto
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
            }).ToList();

            var json = JsonSerializer.Serialize(dtos);
            await _cache.SetStringAsync(cacheKey, json, _studentListCacheOpts);

            return Content(json, "application/json");
        }

        [HttpGet("{code}")]
        public async Task<ActionResult<StudentDto>> GetStudent(string code)
        {
            var s = await _studentService.GetStudentByCodeAsync(code);
            if (s == null) return NotFound($"Student with code '{code}' not found.");

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
            // Resolve BatchCode → Id
            if (string.IsNullOrWhiteSpace(dto.BatchCode))
                return BadRequest("BatchCode is required.");
            var batch = await _batchService.GetBatchByCodeAsync(dto.BatchCode);
            if (batch == null)
                return NotFound($"Batch with code '{dto.BatchCode}' not found.");

            // Resolve GroupCode → Id
            if (string.IsNullOrWhiteSpace(dto.GroupCode))
                return BadRequest("GroupCode is required.");
            var group = await _groupService.GetGroupByCodeAsync(dto.GroupCode);
            if (group == null)
                return NotFound($"Group with code '{dto.GroupCode}' not found.");

            var department = await _context.Departments.FindAsync(batch.DepartmentId);
            var college = await _context.Colleges.FindAsync(department!.CollegeId);

            // Use AuthService to ensure consistent creation (SystemUser + Student)
            var registerDto = new RegisterStudentDto
            {
                FullName = dto.FullName,
                Phone = dto.Phone,
                NationalId = dto.NationalId,
                BatchCode = batch.Code,
                GroupCode = group.Code,
                DepartmentCode = department.Code,
                CollegeCode = college!.Code
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

        [HttpPut("{code}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Update(string code, UpdateStudentDto dto)
        {
            var entity = await _studentService.GetStudentByCodeAsync(code);
            if (entity == null) return NotFound($"Student with code '{code}' not found.");
            try
            {
                await _studentService.UpdateStudentDetailsAsync(entity.Id, dto);
                return NoContent();
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpDelete("{code}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(string code)
        {
            var entity = await _studentService.GetStudentByCodeAsync(code);
            if (entity == null) return NotFound($"Student with code '{code}' not found.");
            try
            {
                await _studentService.DeleteStudentAsync(entity.Id);
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
                var fileId = await fileService.UploadFileStreamAsync(userId, stream, file.FileName, file.ContentType, file.Length);

                jobClient.Enqueue<IBulkUploadJob>(x => x.ProcessStudentDirectUpload(fileId, userId));
                return Accepted(new { JobId = fileId, Message = "File accepted for direct processing" });
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
                var fileId = await fileService.UploadFileStreamAsync(userId, stream, file.FileName, file.ContentType, file.Length);

                jobClient.Enqueue<IBulkUploadJob>(x => x.ProcessStudentAiUpload(fileId, userId));
                return Accepted(new { JobId = fileId, Message = "File accepted for AI processing" });
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
