using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using NUlid;
using UniversityManagementSystem.Core.DTOs;
using UniversityManagementSystem.Core.Entities;
using UniversityManagementSystem.Core.Interfaces;
using UniversityManagementSystem.Infrastructure.Data;

namespace UniversityManagementSystem.Api.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class RegulationsController(
        IRegulationService service,
        IDistributedCache cache,
        AppDbContext context,
        IStorageService storageService,
        IFileService fileService,
        IUserContextService userContext) : ControllerBase
    {
        private readonly IRegulationService _service = service;
        private readonly IDistributedCache _cache = cache;
        private readonly AppDbContext _context = context;
        private readonly IStorageService _storageService = storageService;
        private readonly IFileService _fileService = fileService;
        private readonly IUserContextService _userContext = userContext;
        private const string CacheKey = "Regulations_All";

        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new UniversityManagementSystem.Api.Converters.UlidJsonConverter() }
        };

        // Allowed file MIME types for regulation attachments
        private static readonly HashSet<string> AllowedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "application/pdf",
            "application/msword",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            "application/vnd.ms-excel",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "text/plain"
        };
        private const long MaxFileSizeBytes = 50 * 1024 * 1024; // 50 MB

        // ── Helper: build RegulationDto with public URL ───────────────────────
        private async Task<RegulationDto> ToDto(Regulation r)
        {
            string? fileUrl = null;
            if (r.FileId.HasValue)
            {
                var file = await _context.UploadedFiles.FindAsync(r.FileId.Value);
                if (file != null)
                {
                    // Use the public R2 URL (pub-xxx.r2.dev) — no auth needed, no SigV2/V4 issues
                    fileUrl = _storageService.BuildUrl(file.StorageKey);
                }
            }

            return new RegulationDto
            {
                Id = r.Id,
                Title = r.Title,
                Content = r.Content,
                Type = r.Type.ToString(),
                IsActive = r.IsActive,
                FileId = r.FileId?.ToString(),
                FileUrl = fileUrl,
                DepartmentId = r.DepartmentId?.ToString(),
                Subjects = r.RegulationSubjects?.Select(rs => new RegulationSubjectDto
                {
                    SubjectId = rs.SubjectId,
                    Semester = rs.Semester,
                    IsRequired = rs.IsRequired
                }).ToList() ?? new List<RegulationSubjectDto>()
            };
        }

        // ── GET /api/Regulations ─────────────────────────────────────────────
        [HttpGet]
        public async Task<ActionResult<IEnumerable<RegulationDto>>> GetAll()
        {
            var cached = await _cache.GetStringAsync(CacheKey);
            if (!string.IsNullOrEmpty(cached))
            {
                try
                {
                    return Ok(JsonSerializer.Deserialize<IEnumerable<RegulationDto>>(cached, _jsonOptions));
                }
                catch
                {
                    // If deserialization fails (e.g. outdated cache schema), ignore and fetch from DB
                    await _cache.RemoveAsync(CacheKey);
                }
            }

            var list = await _service.GetAllAsync();
            var dtos = new List<RegulationDto>();
            foreach (var r in list)
                dtos.Add(await ToDto(r));

            await _cache.SetStringAsync(CacheKey, JsonSerializer.Serialize(dtos, _jsonOptions),
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5) });

            return Ok(dtos);
        }

        // ── GET /api/Regulations/active ──────────────────────────────────────
        [HttpGet("active")]
        public async Task<ActionResult<IEnumerable<RegulationDto>>> GetActive()
        {
            var list = await _service.GetActiveAsync();
            var dtos = new List<RegulationDto>();
            foreach (var r in list)
                dtos.Add(await ToDto(r));
            return Ok(dtos);
        }

        // ── GET /api/Regulations/by-code/{code} ──────────────────────────────
        /// <summary>
        /// [PREFERRED] Retrieve a regulation by its auto-generated slug code.
        /// Code is derived from Title, e.g. "General Rules" → "general-rules".
        /// </summary>
        [HttpGet("by-code/{code}")]
        public async Task<IActionResult> GetByCode(string code)
        {
            var regulation = await _service.GetByCodeAsync(code);
            if (regulation == null)
                return NotFound($"Regulation with code '{code}' not found.");

            return Ok(await ToDto(regulation));
        }

        // ── GET /api/Regulations/by-department/{departmentId} ────────────────
        [HttpGet("by-department/{departmentId}")]
        public async Task<IActionResult> GetByDepartment(string departmentId)
        {
            if (!Ulid.TryParse(departmentId, out var deptId))
                return BadRequest("Invalid Department ID.");

            var regulations = await _service.GetByDepartmentAsync(deptId);
            var dtos = new List<RegulationDto>();
            foreach (var r in regulations)
                dtos.Add(await ToDto(r));

            return Ok(dtos);
        }

        // ── GET /api/Regulations/student/{studentId} ─────────────────────────
        [HttpGet("student/{studentId}")]
        public async Task<IActionResult> GetForStudent(string studentId)
        {
            if (!Ulid.TryParse(studentId, out var stuId))
                return BadRequest("Invalid Student ID.");

            var regulation = await _service.GetForStudentAsync(stuId);
            if (regulation == null)
                return NotFound("Student has no regulation assigned.");

            return Ok(await ToDto(regulation));
        }

        // ── POST /api/Regulations ─────────────────────────────────────────────
        /// <summary>
        /// Create a regulation. Supports two modes:
        /// 1) Text-only:          form fields: title, content, type
        /// 2) File attachment:    form fields: title, type + file (PDF/Word/Excel/TXT)
        ///    Content and File can both be provided simultaneously.
        /// NOTE: Code is auto-generated from Title as a URL-safe slug.
        ///       e.g. "General Academic Rules" → "general-academic-rules"
        /// </summary>
        [HttpPost]
        [Authorize(Roles = "Admin,SuperAdmin")]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(52_428_800)] // 50 MB
        public async Task<ActionResult<RegulationDto>> Create([FromForm] CreateRegulationDto dto)
        {
            // Validation: need at least content OR a file
            if (string.IsNullOrWhiteSpace(dto.Content) && dto.File == null)
                return BadRequest("At least one of 'content' or a file attachment must be provided.");

            // Validate file if present
            if (dto.File != null)
            {
                if (!AllowedMimeTypes.Contains(dto.File.ContentType))
                    return BadRequest($"File type '{dto.File.ContentType}' is not allowed. Supported: PDF, DOC, DOCX, XLS, XLSX, TXT.");

                if (dto.File.Length > MaxFileSizeBytes)
                    return BadRequest("File exceeds the 50 MB size limit.");
            }

            // Resolve current user (Admin/SuperAdmin) for file upload
            Ulid? uploadedFileId = null;

            if (dto.File != null)
            {
                var uploaderId = _userContext.GetUserId();

                // Reuse existing FileService — stream upload to R2 "files/" folder
                uploadedFileId = await _fileService.UploadFileStreamAsync(
                    userId: uploaderId,
                    stream: dto.File.OpenReadStream(),
                    fileName: dto.File.FileName,
                    contentType: dto.File.ContentType,
                    fileLength: dto.File.Length);
            }

            var entity = new Regulation
            {
                Title = dto.Title,
                Content = string.IsNullOrWhiteSpace(dto.Content) ? null : dto.Content,
                Type = dto.Type,
                FileId = uploadedFileId,
                IsActive = true,
                DepartmentId = string.IsNullOrEmpty(dto.DepartmentId) ? null : Ulid.Parse(dto.DepartmentId)
            };

            var subjects = new List<RegulationSubject>();
            if (!string.IsNullOrWhiteSpace(dto.SubjectsJson))
            {
                try
                {
                    var parsedSubjects = JsonSerializer.Deserialize<List<RegulationSubjectDto>>(dto.SubjectsJson, _jsonOptions);
                    if (parsedSubjects != null)
                    {
                        foreach (var s in parsedSubjects)
                        {
                            subjects.Add(new RegulationSubject
                            {
                                SubjectId = s.SubjectId,
                                Semester = s.Semester,
                                IsRequired = s.IsRequired
                            });
                        }
                    }
                }
                catch (JsonException ex)
                {
                    return BadRequest($"Invalid JSON format in SubjectsJson: {ex.Message}. Ensure you are sending a valid JSON array.");
                }
            }


            var result = await _service.CreateWithSubjectsAsync(entity, subjects);
            await _cache.RemoveAsync(CacheKey);

            return Ok(await ToDto(result));
        }

        // ── PUT /api/Regulations/{id}  [LEGACY — keep for backward compat] ────
        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(52_428_800)]
        [ApiExplorerSettings(GroupName = "legacy")]
        public async Task<IActionResult> Update(string id, [FromForm] UpdateRegulationDto dto)
        {
            if (!Ulid.TryParse(id, out var regId))
                return BadRequest("Invalid Regulation ID.");

            return await UpdateRegulationCore(regId, dto);
        }

        // ── PUT /api/Regulations/by-code/{code}  [PREFERRED — admin uses code] ─
        /// <summary>
        /// [PREFERRED] Update a regulation by its auto-generated slug code.
        /// Admin/frontend MUST use this route.
        /// </summary>
        [HttpPut("by-code/{code}")]
        [Authorize(Roles = "Admin")]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(52_428_800)]
        public async Task<IActionResult> UpdateByCode(string code, [FromForm] UpdateRegulationDto dto)
        {
            var regulation = await _service.GetByCodeAsync(code);
            if (regulation == null)
                return NotFound($"Regulation with code '{code}' not found.");

            return await UpdateRegulationCore(regulation.Id, dto);
        }

        // ── DELETE /api/Regulations/{id}  [LEGACY — keep for backward compat] ──
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        [ApiExplorerSettings(GroupName = "legacy")]
        public async Task<IActionResult> Delete(string id)
        {
            if (!Ulid.TryParse(id, out var regId))
                return BadRequest("Invalid Regulation ID.");

            await _service.DeleteAsync(regId);
            await _cache.RemoveAsync(CacheKey);
            return NoContent();
        }

        // ── DELETE /api/Regulations/by-code/{code}  [PREFERRED] ──────────────
        /// <summary>
        /// [PREFERRED] Delete a regulation by its auto-generated slug code.
        /// Admin/frontend MUST use this route.
        /// </summary>
        [HttpDelete("by-code/{code}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteByCode(string code)
        {
            var regulation = await _service.GetByCodeAsync(code);
            if (regulation == null)
                return NotFound($"Regulation with code '{code}' not found.");

            await _service.DeleteAsync(regulation.Id);
            await _cache.RemoveAsync(CacheKey);
            return NoContent();
        }

        // ── Shared update logic ───────────────────────────────────────────────
        private async Task<IActionResult> UpdateRegulationCore(Ulid regId, UpdateRegulationDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Content) && dto.File == null)
                return BadRequest("At least one of 'content' or a file attachment must be provided.");

            Ulid? uploadedFileId = null;

            if (dto.File != null)
            {
                if (dto.File.Length > MaxFileSizeBytes)
                    return BadRequest("File exceeds the 50 MB size limit.");

                if (!AllowedMimeTypes.Contains(dto.File.ContentType))
                    return BadRequest($"File type '{dto.File.ContentType}' is not allowed.");

                var uploaderId = _userContext.GetUserId();
                uploadedFileId = await _fileService.UploadFileStreamAsync(
                    userId: uploaderId,
                    stream: dto.File.OpenReadStream(),
                    fileName: dto.File.FileName,
                    contentType: dto.File.ContentType,
                    fileLength: dto.File.Length);
            }

            var entity = new Regulation
            {
                Title    = dto.Title,
                Content  = string.IsNullOrWhiteSpace(dto.Content) ? null : dto.Content,
                Type     = dto.Type,
                FileId   = uploadedFileId,
                IsActive = dto.IsActive,
                DepartmentId = string.IsNullOrEmpty(dto.DepartmentId) ? null : Ulid.Parse(dto.DepartmentId)
            };

            List<RegulationSubject>? subjects = null;
            if (dto.SubjectsJson != null)
            {
                if (string.IsNullOrWhiteSpace(dto.SubjectsJson))
                {
                    subjects = new List<RegulationSubject>(); // explicit empty
                }
                else
                {
                    try
                    {
                        subjects = new List<RegulationSubject>();
                        var parsedSubjects = JsonSerializer.Deserialize<List<RegulationSubjectDto>>(dto.SubjectsJson, _jsonOptions);
                        if (parsedSubjects != null)
                        {
                            foreach (var s in parsedSubjects)
                            {
                                subjects.Add(new RegulationSubject
                                {
                                    SubjectId = s.SubjectId,
                                    Semester = s.Semester,
                                    IsRequired = s.IsRequired
                                });
                            }
                        }
                    }
                    catch (JsonException ex)
                    {
                        return BadRequest($"Invalid JSON format in SubjectsJson: {ex.Message}. Ensure you are sending a valid JSON array.");
                    }
                }
            }

            await _service.UpdateWithSubjectsAsync(regId, entity, subjects);
            await _cache.RemoveAsync(CacheKey);
            return NoContent();
        }
    }
}
