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
        IDistributedCache cache,
        IUserContextService userContext) : ControllerBase
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
        private readonly IUserContextService _userContext = userContext;

        // ── GET /api/students/search?q= ──────────────────────────────────────
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
                .Select(s => new { s.Id, s.Code, s.FullName, s.Email, s.UniversityStudentId, s.BatchId, s.IsActive })
                .ToListAsync();

            return Ok(results);
        }

        // ── GET /api/students/filter ─────────────────────────────────────────
        /// <summary>
        /// Filtered, paginated list of students with full enriched info.
        /// All query params are optional and composable.
        /// Example: GET /api/students/filter?departmentId=X&batchId=Y&page=1&size=25
        /// </summary>
        [HttpGet("filter")]
        [Authorize(Roles = "Admin,Doctor,TeachingAssistant,SuperAdmin")]
        public async Task<IActionResult> Filter([FromQuery] StudentFilterDto f)
        {
            var page = Math.Max(1, f.Page);
            var size = Math.Clamp(f.Size, 1, 100);

            var query = _context.Students
                .AsNoTracking()
                .Include(s => s.SystemUser)
                .Include(s => s.College)
                .Include(s => s.Department)
                .Include(s => s.Batch)
                .Include(s => s.Group)
                .AsQueryable();

            if (f.UniversityId.HasValue)  query = query.Where(s => s.UniversityId  == f.UniversityId.Value);
            if (f.CollegeId.HasValue)     query = query.Where(s => s.CollegeId     == f.CollegeId.Value);
            if (f.DepartmentId.HasValue)  query = query.Where(s => s.DepartmentId  == f.DepartmentId.Value);
            if (f.BatchId.HasValue)       query = query.Where(s => s.BatchId       == f.BatchId.Value);
            if (f.GroupId.HasValue)       query = query.Where(s => s.GroupId       == f.GroupId.Value);
            if (f.IsActive.HasValue)      query = query.Where(s => s.IsActive      == f.IsActive.Value);

            if (!string.IsNullOrWhiteSpace(f.Search))
            {
                var pattern = $"%{f.Search.Trim()}%";
                query = query.Where(s =>
                    EF.Functions.ILike(s.FullName, pattern) ||
                    EF.Functions.ILike(s.UniversityStudentId, pattern) ||
                    EF.Functions.ILike(s.Email, pattern) ||
                    EF.Functions.ILike(s.Code, pattern));
            }

            var total = await query.CountAsync();

            var data = await query
                .OrderBy(s => s.FullName)
                .Skip((page - 1) * size)
                .Take(size)
                .Select(s => new StudentDetailDto
                {
                    Id                  = s.Id,
                    Code                = s.Code,
                    FullName            = s.FullName,
                    Email               = s.Email,
                    UniversityEmail     = s.SystemUser != null ? s.SystemUser.UniversityEmail : "",
                    Phone               = s.Phone,
                    NationalId          = s.SystemUser != null ? s.SystemUser.NationalId : "",
                    UniversityStudentId = s.UniversityStudentId,
                    IsActive            = s.IsActive,
                    UniversityId        = s.UniversityId,
                    CollegeId           = s.CollegeId,
                    CollegeName         = s.College != null ? s.College.Name : "",
                    DepartmentId        = s.DepartmentId,
                    DepartmentName      = s.Department != null ? s.Department.Name : "",
                    BatchId             = s.BatchId,
                    BatchName           = s.Batch != null ? s.Batch.Name : "",
                    GroupId             = s.GroupId,
                    GroupName           = s.Group != null ? s.Group.Name : ""
                })
                .ToListAsync();

            return Ok(new PagedResult<StudentDetailDto>
            {
                Data       = data,
                TotalCount = total,
                Page       = page,
                Size       = size
            });
        }

        // ── GET /api/students ────────────────────────────────────────────────
        [HttpGet]
        public async Task<ActionResult<IEnumerable<StudentDto>>> GetAll([FromQuery] int page = 1, [FromQuery] int size = 10)
        {
            var cacheKey = $"{StudentListCachePrefix}{page}:size:{size}";
            try
            {
                var cached = await _cache.GetStringAsync(cacheKey);
                if (!string.IsNullOrEmpty(cached))
                    return Content(cached, "application/json");
            }
            catch { }

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
                UniversityId = s.UniversityId,
                BatchId = s.BatchId,
                GroupId = s.GroupId,
                IsActive = s.IsActive
            }).ToList();

            var json = JsonSerializer.Serialize(dtos);
            try { await _cache.SetStringAsync(cacheKey, json, _studentListCacheOpts); } catch { }
            return Content(json, "application/json");
        }

        // ── GET /api/students/{code} ─────────────────────────────────────────
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
                UniversityId = s.UniversityId,
                BatchId = s.BatchId,
                GroupId = s.GroupId,
                IsActive = s.IsActive
            });
        }

        // ── PATCH /api/students/{id} ─────────────────────────────────────────
        /// <summary>
        /// Partial update — only the non-null fields you send will be changed.
        /// Example: send only { "isActive": false } to deactivate without touching other fields.
        /// </summary>
        [HttpPatch("{id}")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<IActionResult> Patch(string id, [FromBody] PatchStudentDto dto)
        {
            if (!Ulid.TryParse(id, out var studentId))
                return BadRequest("Invalid student ID format.");

            var student = await _context.Students
                .Include(s => s.SystemUser)
                .FirstOrDefaultAsync(s => s.Id == studentId);

            if (student == null) return NotFound($"Student with ID '{id}' not found.");

            if (dto.FullName  != null) student.FullName = dto.FullName;
            if (dto.Phone     != null) student.Phone    = dto.Phone;
            if (dto.Email     != null) student.Email    = dto.Email;
            if (dto.IsActive.HasValue) student.IsActive = dto.IsActive.Value;

            if (dto.BatchCode != null)
            {
                var batch = await _context.Batches.FirstOrDefaultAsync(b => b.Code == dto.BatchCode);
                if (batch == null) return NotFound($"Batch with code '{dto.BatchCode}' not found.");
                student.BatchId = batch.Id;
            }

            if (dto.GroupCode != null)
            {
                var group = await _context.Groups.FirstOrDefaultAsync(g => g.Code == dto.GroupCode);
                if (group == null) return NotFound($"Group with code '{dto.GroupCode}' not found.");
                student.GroupId = group.Id;
            }

            await _context.SaveChangesAsync();
            return NoContent();
        }

        // ── GET /api/students/by-batch/{batchId} ─────────────────────────────
        [HttpGet("by-batch/{batchId}")]
        [Authorize(Roles = "Admin,Doctor,TeachingAssistant,SuperAdmin")]
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
                UniversityId = s.UniversityId,
                BatchId = s.BatchId,
                GroupId = s.GroupId,
                IsActive = s.IsActive
            }));
        }

        // ── POST /api/students ───────────────────────────────────────────────
        [HttpPost]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<ActionResult<StudentDto>> Create(CreateStudentDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.BatchCode))
                return BadRequest("BatchCode is required.");
            var batch = await _batchService.GetBatchByCodeAsync(dto.BatchCode);
            if (batch == null) return NotFound($"Batch with code '{dto.BatchCode}' not found.");

            if (string.IsNullOrWhiteSpace(dto.GroupCode))
                return BadRequest("GroupCode is required.");
            var group = await _groupService.GetGroupByCodeAsync(dto.GroupCode);
            if (group == null) return NotFound($"Group with code '{dto.GroupCode}' not found.");

            var department = await _context.Departments.FindAsync(batch.DepartmentId);
            var college    = await _context.Colleges.FindAsync(department!.CollegeId);

            var registerDto = new RegisterStudentDto
            {
                FullName            = dto.FullName,
                Phone               = dto.Phone,
                NationalId          = dto.NationalId,
                BatchCode           = batch.Code,
                GroupCode           = group.Code,
                DepartmentCode      = department.Code,
                CollegeCode         = college!.Code,
                UniversityStudentId = dto.UniversityStudentId
            };

            var creatorId = _userContext.GetUserId();
            var authResponse = await _authService.RegisterStudentAsync(registerDto, creatorId);

            var student = await _studentService.GetStudentByUniversityEmailAsync(authResponse.UniversityEmail!);
            if (student == null) return BadRequest("Failed to retrieve created student");

            return Ok(new StudentDto
            {
                Id                  = student.Id,
                Code                = student.Code,
                FullName            = student.FullName,
                Email               = student.Email,
                Phone               = student.Phone,
                NationalId          = registerDto.NationalId,
                UniversityStudentId = student.UniversityStudentId,
                UniversityEmail     = authResponse.UniversityEmail ?? "N/A",
                UniversityId        = student.UniversityId,
                BatchId             = student.BatchId,
                GroupId             = student.GroupId,
                IsActive            = student.IsActive
            });
        }

        // ── PUT /api/students/{code} ─────────────────────────────────────────
        [HttpPut("{code}")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<IActionResult> Update(string code, UpdateStudentDto dto)
        {
            var entity = await _studentService.GetStudentByCodeAsync(code);
            if (entity == null) return NotFound($"Student with code '{code}' not found.");
            try
            {
                await _studentService.UpdateStudentDetailsAsync(entity.Id, dto);
                return NoContent();
            }
            catch (Exception ex) { return BadRequest(ex.Message); }
        }

        // ── DELETE /api/students/{code} ──────────────────────────────────────
        [HttpDelete("{code}")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<IActionResult> Delete(string code)
        {
            var entity = await _studentService.GetStudentByCodeAsync(code);
            if (entity == null) return NotFound($"Student with code '{code}' not found.");
            try
            {
                await _studentService.DeleteStudentAsync(entity.Id);
                return NoContent();
            }
            catch (Exception ex) { return BadRequest(ex.Message); }
        }

        // ── GET /api/students/by-offering/{offeringId} ───────────────────────
        /// <summary>
        /// Returns all students enrolled in a specific subject offering.
        /// Doctors see only their own offerings; Admins see all.
        /// Paginated: ?page=1&size=20
        /// </summary>
        [HttpGet("by-offering/{offeringId}")]
        [Authorize(Roles = "Admin,Doctor,SuperAdmin")]
        public async Task<IActionResult> GetByOffering(
            string offeringId,
            [FromQuery] int page = 1,
            [FromQuery] int size = 20)
        {
            if (!Ulid.TryParse(offeringId, out var oId))
                return BadRequest("Invalid Offering ID.");

            page = Math.Max(1, page);
            size = Math.Clamp(size, 1, 100);

            // Doctors may only query their own offerings
            var roleClaim = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value
                         ?? User.FindFirst("role")?.Value ?? "";
            if (roleClaim.Equals("Doctor", StringComparison.OrdinalIgnoreCase))
            {
                var userIdClaim = User.FindFirst("nameid")?.Value;
                if (userIdClaim == null) return Unauthorized();
                var doctorUserId = Ulid.Parse(userIdClaim);
                var isOwner = await _context.SubjectOfferings
                    .AsNoTracking()
                    .AnyAsync(o => o.Id == oId && o.Doctor.SystemUserId == doctorUserId);
                if (!isOwner) return Forbid();
            }

            var query = _context.Enrollments
                .AsNoTracking()
                .Where(e => e.SubjectOfferingId == oId && e.IsActive && e.DeletedAt == null)
                .Select(e => e.Student);

            var total = await query.CountAsync();

            var data = await query
                .OrderBy(s => s.FullName)
                .Skip((page - 1) * size)
                .Take(size)
                .Select(s => new StudentSummaryDto
                {
                    Id                  = s.Id.ToString(),
                    Code                = s.Code,
                    FullName            = s.FullName,
                    UniversityStudentId = s.UniversityStudentId,
                    Email               = s.Email,
                    BatchName           = s.Batch != null ? s.Batch.Name : "",
                    DepartmentName      = s.Department != null ? s.Department.Name : "",
                    CollegeName         = s.College != null ? s.College.Name : "",
                })
                .ToListAsync();

            return Ok(new PagedResult<StudentSummaryDto>
            {
                Data       = data,
                TotalCount = total,
                Page       = page,
                Size       = size,
            });
        }

        // ── GET /api/students/struggling ─────────────────────────────────────
        /// <summary>
        /// Returns students with average grade points below a threshold.
        /// Optional: ?threshold=2.0&departmentId=&batchId=&page=1&size=20
        /// Default threshold is 2.0 (D grade equivalent).
        /// Admin only.
        /// </summary>
        [HttpGet("struggling")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<IActionResult> GetStrugglingStudents(
            [FromQuery] double  threshold    = 2.0,
            [FromQuery] string? departmentId = null,
            [FromQuery] string? batchId      = null,
            [FromQuery] int     page         = 1,
            [FromQuery] int     size         = 20)
        {
            page = Math.Max(1, page);
            size = Math.Clamp(size, 1, 100);

            // Average finalized grade points per student
            var lowGpaQuery = _context.StudentGrades
                .AsNoTracking()
                .Where(g => g.IsFinalized && g.DeletedAt == null)
                .GroupBy(g => g.StudentId)
                .Where(g => g.Average(x => x.GradePoints) < threshold)
                .Select(g => g.Key);

            var query = _context.Students
                .AsNoTracking()
                .Where(s => s.DeletedAt == null && lowGpaQuery.Contains(s.Id));

            if (!string.IsNullOrWhiteSpace(departmentId) && Ulid.TryParse(departmentId, out var dId))
                query = query.Where(s => s.DepartmentId == dId);

            if (!string.IsNullOrWhiteSpace(batchId) && Ulid.TryParse(batchId, out var bId))
                query = query.Where(s => s.BatchId == bId);

            var total = await query.CountAsync();

            var data = await query
                .OrderBy(s => s.FullName)
                .Skip((page - 1) * size)
                .Take(size)
                .Select(s => new StudentSummaryDto
                {
                    Id                  = s.Id.ToString(),
                    Code                = s.Code,
                    FullName            = s.FullName,
                    UniversityStudentId = s.UniversityStudentId,
                    Email               = s.Email,
                    BatchName           = s.Batch != null ? s.Batch.Name : "",
                    DepartmentName      = s.Department != null ? s.Department.Name : "",
                    CollegeName         = s.College != null ? s.College.Name : "",
                })
                .ToListAsync();

            return Ok(new PagedResult<StudentSummaryDto>
            {
                Data       = data,
                TotalCount = total,
                Page       = page,
                Size       = size,
            });
        }

        [HttpPost("bulk-upload-direct")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> BulkUploadDirect(IFormFile file, [FromServices] IFileService fileService, [FromServices] IBackgroundJobClient jobClient)
        {
            if (file == null || file.Length == 0) return BadRequest("File is empty");
            var userId = _userContext.GetUserId();
            using var stream = file.OpenReadStream();
            var fileId = await fileService.UploadFileStreamAsync(userId, stream, file.FileName, file.ContentType, file.Length);
            jobClient.Enqueue<IBulkUploadJob>(x => x.ProcessStudentDirectUpload(fileId, userId));
            return Accepted(new { JobId = fileId, Message = "File accepted for direct processing" });
        }

        [HttpPost("bulk-upload-ai")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> BulkUploadAi(IFormFile file, [FromServices] IFileService fileService, [FromServices] IBackgroundJobClient jobClient)
        {
            if (file == null || file.Length == 0) return BadRequest("File is empty");
            var userId = _userContext.GetUserId();
            using var stream = file.OpenReadStream();
            var fileId = await fileService.UploadFileStreamAsync(userId, stream, file.FileName, file.ContentType, file.Length);
            jobClient.Enqueue<IBulkUploadJob>(x => x.ProcessStudentAiUpload(fileId, userId));
            return Accepted(new { JobId = fileId, Message = "File accepted for AI processing" });
        }

        [HttpPost("import-excel")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> ImportFromExcel(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("No file uploaded. Please attach an .xlsx file.");
            var ext = System.IO.Path.GetExtension(file.FileName);
            if (!ext.Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
                return BadRequest($"Invalid file type '{ext}'. Only .xlsx files are accepted.");
            var result = await _excelImportService.ImportStudentsFromExcelAsync(file);
            return Ok(result);
        }

        [HttpPost("import-excel/download-credentials")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> ImportAndDownloadCredentials(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("No file uploaded. Please attach an .xlsx file.");
            var ext = System.IO.Path.GetExtension(file.FileName);
            if (!ext.Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
                return BadRequest($"Invalid file type '{ext}'. Only .xlsx files are accepted.");

            var result = await _excelImportService.ImportStudentsFromExcelAsync(file);

            // Always include import summary in header so frontend can show it even on JSON fallback
            Response.Headers.Append("X-Import-Summary",
                $"imported={result.Imported};skipped={result.Skipped};total={result.TotalRows};warnings={result.Warnings.Count}");

            if (result.ImportedCredentials.Count == 0)
            {
                // No new students — return JSON with full details so frontend can show errors/warnings
                return Ok(new
                {
                    result.TotalRows, result.Imported, result.Skipped,
                    result.Errors, result.Warnings,
                    message = result.Skipped > 0
                        ? $"All {result.Skipped} rows were skipped. Check errors for details."
                        : "No students were imported."
                });
            }

            var universityName = await _context.Universities
                .AsNoTracking()
                .Select(u => u.Name)
                .FirstOrDefaultAsync() ?? "University";

            var excelBytes = await _excelImportService.GenerateCredentialsExcelAsync(
                result.ImportedCredentials, universityName);

            var fileName = $"credentials_{DateTime.UtcNow:yyyyMMdd_HHmm}.xlsx";
            return File(excelBytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                fileName);
        }

        [HttpGet("import-excel/template")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<IActionResult> DownloadImportTemplate()
        {
            var universityName = await _context.Universities
                .AsNoTracking()
                .Select(u => u.Name)
                .FirstOrDefaultAsync() ?? "University";

            var batches = await _context.Batches
                .AsNoTracking()
                .Include(b => b.Department)
                .Select(b => new { b.Code, DeptName = b.Department.Name })
                .Take(5)
                .ToListAsync();

            var groups = await _context.Groups
                .AsNoTracking()
                .Select(g => g.Code)
                .Take(5)
                .ToListAsync();

            using var workbook = new ClosedXML.Excel.XLWorkbook();
            var ws = workbook.Worksheets.Add("Students");

            // ── Title ──────────────────────────────────────────────────────────
            ws.Cell(1, 1).Value = $"{universityName} — Student Import Template";
            ws.Range(1, 1, 1, 11).Merge();
            ws.Cell(1, 1).Style.Font.Bold = true;
            ws.Cell(1, 1).Style.Font.FontSize = 13;
            ws.Cell(1, 1).Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.FromHtml("#1E3A5F");
            ws.Cell(1, 1).Style.Font.FontColor = ClosedXML.Excel.XLColor.White;
            ws.Cell(1, 1).Style.Alignment.Horizontal = ClosedXML.Excel.XLAlignmentHorizontalValues.Center;

            // ── Headers row 2 ──────────────────────────────────────────────────
            var required = new[] { "FullName*", "NationalId*", "Phone*", "BatchCode*", "GroupCode*" };
            var optional = new[] { "Email", "Governorate", "Gender", "DateOfBirth", "StudentType", "Religion" };
            var all = required.Concat(optional).ToArray();

            for (int c = 0; c < all.Length; c++)
            {
                var cell = ws.Cell(2, c + 1);
                cell.Value = all[c];
                cell.Style.Font.Bold = true;
                bool isReq = c < required.Length;
                cell.Style.Fill.BackgroundColor = isReq
                    ? ClosedXML.Excel.XLColor.FromHtml("#C00000")
                    : ClosedXML.Excel.XLColor.FromHtml("#2E75B6");
                cell.Style.Font.FontColor = ClosedXML.Excel.XLColor.White;
                cell.Style.Alignment.Horizontal = ClosedXML.Excel.XLAlignmentHorizontalValues.Center;
            }

            // ── Sample rows ────────────────────────────────────────────────────
            var sampleBatch = batches.FirstOrDefault()?.Code ?? "BATCH2024";
            var sampleGroup = groups.FirstOrDefault() ?? "GRP-A";
            object[][] samples =
            [
                ["Ahmed Ali Mohamed", "30501011234567", "01012345678", sampleBatch, sampleGroup, "ahmed@gmail.com", "Cairo", "Male", "2000-05-15", "Regular", "Islam"],
                ["Sara Mohamed Hassan", "30601021234568", "01198765432", sampleBatch, sampleGroup, "", "Giza", "Female", "2001-03-22", "Regular", ""],
                ["Omar Khaled Ibrahim", "30701031234569", "01556789012", sampleBatch, sampleGroup, "", "", "", "", "", ""],
            ];

            for (int r = 0; r < samples.Length; r++)
            {
                bool isEven = r % 2 == 0;
                for (int c = 0; c < samples[r].Length; c++)
                {
                    var cell = ws.Cell(r + 3, c + 1);
                    cell.Value = samples[r][c]?.ToString() ?? "";
                    if (isEven)
                        cell.Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.FromHtml("#EBF3FB");
                }
            }

            // ── Legend ─────────────────────────────────────────────────────────
            int leg = samples.Length + 4;
            ws.Cell(leg, 1).Value = "* Required   |   Gender: Male / Female   |   DateOfBirth: YYYY-MM-DD   |   StudentType: Regular / Transfer / Repeating / External";
            ws.Range(leg, 1, leg, 11).Merge();
            ws.Cell(leg, 1).Style.Font.Italic = true;
            ws.Cell(leg, 1).Style.Font.FontSize = 9;
            ws.Cell(leg, 1).Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.FromHtml("#FFF2CC");

            // ── Batches reference sheet ────────────────────────────────────────
            var refWs = workbook.Worksheets.Add("Reference - Batches & Groups");
            refWs.Cell(1, 1).Value = "BatchCode";
            refWs.Cell(1, 2).Value = "Department";
            refWs.Cell(1, 1).Style.Font.Bold = true;
            refWs.Cell(1, 2).Style.Font.Bold = true;
            for (int i = 0; i < batches.Count; i++)
            {
                refWs.Cell(i + 2, 1).Value = batches[i].Code;
                refWs.Cell(i + 2, 2).Value = batches[i].DeptName;
            }
            refWs.Cell(1, 4).Value = "GroupCode";
            refWs.Cell(1, 4).Style.Font.Bold = true;
            for (int i = 0; i < groups.Count; i++)
                refWs.Cell(i + 2, 4).Value = groups[i];

            // ── Column widths ──────────────────────────────────────────────────
            int[] widths = [28, 18, 15, 14, 12, 26, 14, 10, 14, 14, 12];
            for (int i = 0; i < widths.Length; i++)
                ws.Column(i + 1).Width = widths[i];

            ws.SheetView.Freeze(2, 0);

            using var ms = new System.IO.MemoryStream();
            workbook.SaveAs(ms);
            return File(ms.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "student_import_template.xlsx");
        }
    }
}
