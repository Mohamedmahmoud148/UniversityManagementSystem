using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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
using UniversityManagementSystem.Infrastructure.Data;

namespace UniversityManagementSystem.Api.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class SubjectsController(
        ISubjectService service,
        IDistributedCache cache,
        AppDbContext context,
        IDepartmentService departmentService,
        ICollegeService collegeService,
        IBatchService batchService,
        IDoctorService doctorService) : ControllerBase
    {
        private readonly ISubjectService _service = service;
        private readonly IDistributedCache _cache = cache;
        private readonly AppDbContext _context = context;
        private readonly IDepartmentService _departmentService = departmentService;
        private readonly ICollegeService _collegeService = collegeService;
        private readonly IBatchService _batchService = batchService;
        private readonly IDoctorService _doctorService = doctorService;
        private const string CachePrefix = "Subjects_Batch_";

        // ── GET /api/subjects/search?name={query} ────────────────────────────
        [HttpGet("search")]
        public async Task<IActionResult> SearchSubjects([FromQuery] string? name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return BadRequest("Search query is required.");

            var pattern = $"%{name}%";

            var results = await _context.Subjects
                .AsNoTracking()
                .Where(s =>
                    EF.Functions.ILike(s.Name, pattern) ||
                    EF.Functions.ILike(s.Code, pattern))
                .OrderBy(s => s.Name)
                .Take(20)
                .Select(s => new SubjectSearchDto(s.Id.ToString(), s.Name, s.Code))
                .ToListAsync();

            return Ok(results);
        }

        private static SubjectDto MapToDto(Subject s)
        {
            // Pick the first assigned doctor's name (if any)
            var doctorName = s.SubjectDoctors?.FirstOrDefault()?.Doctor?.FullName;
            return new SubjectDto(
                s.Id,
                s.Name,
                s.Code,
                s.CreditHours,
                s.CollegeId,
                s.College?.Name,
                s.DepartmentId,
                s.Department?.Name ?? string.Empty,
                s.BatchId,
                s.Batch?.Name,
                doctorName
            );
        }

        [HttpGet("by-batch/{batchId}")]
        public async Task<ActionResult<IEnumerable<SubjectDto>>> GetByBatch(string batchId)
        {
            if (!Ulid.TryParse(batchId, out var bId)) return BadRequest("Invalid Batch ID.");
            var cacheKey = $"{CachePrefix}{batchId}";
            var cachedData = await _cache.GetStringAsync(cacheKey);
            if (!string.IsNullOrEmpty(cachedData))
            {
                return Ok(JsonSerializer.Deserialize<IEnumerable<SubjectDto>>(cachedData));
            }

            var list = await _service.GetSubjectsByBatchIdAsync(bId);
            var dtos = list.Select(MapToDto);

            await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(dtos), new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
            });

            return Ok(dtos);
        }

        [HttpGet("{code}")]
        public async Task<ActionResult<SubjectDto>> GetByCode(string code)
        {
            var s = await _service.GetSubjectByCodeAsync(code);
            if (s == null) return NotFound($"Subject with code '{code}' not found.");

            return Ok(MapToDto(s));
        }

        [HttpGet("by-department/{departmentId}")]
        public async Task<ActionResult<IEnumerable<SubjectDto>>> GetByDepartment(string departmentId)
        {
            if (!Ulid.TryParse(departmentId, out var deptId)) return BadRequest("Invalid Department ID.");
            var list = await _service.GetSubjectsByDepartmentIdAsync(deptId);
            return Ok(list.Select(MapToDto));
        }

        [HttpGet("by-college/{collegeId}")]
        public async Task<ActionResult<IEnumerable<SubjectDto>>> GetByCollege(string collegeId)
        {
            if (!Ulid.TryParse(collegeId, out var colId)) return BadRequest("Invalid College ID.");
            var list = await _service.GetSubjectsByCollegeIdAsync(colId);
            return Ok(list.Select(MapToDto));
        }

        [HttpGet("my-subjects")]
        public async Task<ActionResult<IEnumerable<SubjectDto>>> GetMySubjects()
        {
            // Resolve if Doctor or Student
            if (User.IsInRole("Doctor"))
            {
                // ProfileId = Doctor entity ULID (set in JWT at login)
                // Using ProfileId avoids the incorrect SystemUser-ID → Doctor-ID mismatch
                var profileIdClaim = User.FindFirst("ProfileId");
                if (profileIdClaim == null) return Unauthorized("Doctor profile not found.");
                if (!Ulid.TryParse(profileIdClaim.Value, out var doctorId)) return Unauthorized("Invalid doctor ID.");

                var subjects = await _service.GetDoctorSubjectsAsync(doctorId);
                return Ok(subjects.Select(MapToDto));
            }
            else if (User.IsInRole("Student"))
            {
                var profileIdClaim = User.FindFirst("ProfileId");
                if (profileIdClaim == null) return Unauthorized("Student profile not found.");
                if (!Ulid.TryParse(profileIdClaim.Value, out var studentId)) return Unauthorized("Invalid student ID.");

                var subjects = await _service.GetStudentSubjectsAsync(studentId);
                return Ok(subjects.Select(MapToDto));
            }

            return Forbid();
        }

        [HttpPost]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<ActionResult<SubjectDto>> Create(CreateSubjectDto dto)
        {
            // Resolve DepartmentCode → Id
            if (string.IsNullOrWhiteSpace(dto.DepartmentCode))
                return BadRequest("DepartmentCode is required.");
            var department = await _departmentService.GetDepartmentByCodeAsync(dto.DepartmentCode);
            if (department == null)
                return NotFound($"Department with code '{dto.DepartmentCode}' not found.");

            // Resolve optional CollegeCode → Id
            Ulid? collegeId = null;
            if (!string.IsNullOrWhiteSpace(dto.CollegeCode))
            {
                var college = await _collegeService.GetCollegeByCodeAsync(dto.CollegeCode);
                if (college == null)
                    return NotFound($"College with code '{dto.CollegeCode}' not found.");
                collegeId = college.Id;
            }

            // Resolve optional BatchCode → Id
            Ulid? batchId = null;
            if (!string.IsNullOrWhiteSpace(dto.BatchCode))
            {
                var batch = await _batchService.GetBatchByCodeAsync(dto.BatchCode);
                if (batch == null)
                    return NotFound($"Batch with code '{dto.BatchCode}' not found.");
                batchId = batch.Id;
            }

            var entity = new Subject
            {
                Name = dto.Name,
                Code = dto.Code,
                CreditHours = dto.CreditHours,
                CollegeId = collegeId,
                DepartmentId = department.Id,
                BatchId = batchId
            };
            var result = await _service.CreateSubjectAsync(entity);

            // Invalidate relevant batch cache
            if (batchId.HasValue)
                await _cache.RemoveAsync($"{CachePrefix}{batchId}");

            // Need to reload with Includes to map names properly for the response
            var loadedSubject = await _service.GetSubjectByCodeAsync(result.Code);

            return Ok(MapToDto(loadedSubject!));
        }

        [HttpPut("{code}")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<IActionResult> Update(string code, UpdateSubjectDto dto)
        {
            var entity = await _service.GetSubjectByCodeAsync(code);
            if (entity == null) return NotFound($"Subject with code '{code}' not found.");
            await _service.UpdateSubjectDetailsAsync(entity.Id, dto);
            return NoContent();
        }

        [HttpDelete("{code}")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<IActionResult> Delete(string code)
        {
            var entity = await _service.GetSubjectByCodeAsync(code);
            if (entity == null) return NotFound($"Subject with code '{code}' not found.");
            try
            {
                await _service.DeleteSubjectAsync(entity.Id);
                return NoContent();
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // ── LEGACY (kept for AI/internal use): assign by internal ULID ─────────
        [HttpPut("assign-doctor-by-id")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        [ApiExplorerSettings(GroupName = "legacy")]
        public async Task<IActionResult> AssignDoctorById(string subjectId, string doctorId)
        {
            if (!Ulid.TryParse(subjectId, out var sId)) return BadRequest("Invalid Subject ID.");
            if (!Ulid.TryParse(doctorId, out var dId)) return BadRequest("Invalid Doctor ID.");
            await _service.AssignSubjectToDoctorAsync(sId, dId);
            return NoContent();
        }

        // ── NEW (preferred): Admin routes using public Codes ──────────────────
        /// <summary>
        /// [PREFERRED] Assign a doctor to a subject using public Codes.
        /// Admin/frontend MUST use this route. ULIDs are resolved internally.
        /// </summary>
        [HttpPut("assign-doctor")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<IActionResult> AssignDoctor([FromQuery] string subjectCode, [FromQuery] string doctorCode)
        {
            if (string.IsNullOrWhiteSpace(subjectCode)) return BadRequest("subjectCode is required.");
            if (string.IsNullOrWhiteSpace(doctorCode))  return BadRequest("doctorCode is required.");

            // Resolve codes → internal ULIDs
            var subject = await _service.GetSubjectByCodeAsync(subjectCode);
            if (subject == null) return NotFound($"Subject with code '{subjectCode}' not found.");

            var doctor = await _doctorService.GetDoctorByCodeAsync(doctorCode);
            if (doctor == null) return NotFound($"Doctor with code '{doctorCode}' not found.");

            try
            {
                await _service.AssignSubjectToDoctorAsync(subject.Id, doctor.Id);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
            return Ok(new
            {
                message   = "Doctor assigned successfully.",
                subjectId = subject.Id.ToString(),
                subjectCode,
                subjectName = subject.Name,
                doctorId   = doctor.Id.ToString(),
                doctorCode,
                doctorName = doctor.FullName
            });
        }

        // ── LEGACY (kept for AI/internal use): assign assistant by internal ULID
        [HttpPut("assign-assistant-by-id")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        [ApiExplorerSettings(GroupName = "legacy")]
        public async Task<IActionResult> AssignAssistantById(string subjectId, string assistantId)
        {
            if (!Ulid.TryParse(subjectId, out var sId)) return BadRequest("Invalid Subject ID.");
            if (!Ulid.TryParse(assistantId, out var aId)) return BadRequest("Invalid Assistant ID.");
            await _service.AssignSubjectToAssistantAsync(sId, aId);
            return NoContent();
        }

        /// <summary>
        /// [PREFERRED] Assign a teaching assistant to a subject using public Codes.
        /// Admin/frontend MUST use this route. ULIDs are resolved internally.
        /// </summary>
        [HttpPut("assign-assistant")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<IActionResult> AssignAssistant([FromQuery] string subjectCode, [FromQuery] string assistantCode)
        {
            if (string.IsNullOrWhiteSpace(subjectCode))   return BadRequest("subjectCode is required.");
            if (string.IsNullOrWhiteSpace(assistantCode)) return BadRequest("assistantCode is required.");

            // Resolve subject code → ULID
            var subject = await _service.GetSubjectByCodeAsync(subjectCode);
            if (subject == null) return NotFound($"Subject with code '{subjectCode}' not found.");

            // Resolve assistant code via context (TeachingAssistants share Code on BaseEntity)
            var assistant = await _context.TeachingAssistants
                .AsNoTracking()
                .FirstOrDefaultAsync(ta => ta.Code == assistantCode);
            if (assistant == null) return NotFound($"Teaching Assistant with code '{assistantCode}' not found.");

            await _service.AssignSubjectToAssistantAsync(subject.Id, assistant.Id);
            return Ok(new
            {
                message       = "Assistant assigned successfully.",
                subjectId     = subject.Id.ToString(),
                subjectCode,
                subjectName   = subject.Name,
                assistantId   = assistant.Id.ToString(),
                assistantCode
            });
        }
    }
}
