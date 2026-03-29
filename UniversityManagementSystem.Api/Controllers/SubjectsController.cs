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
        IBatchService batchService) : ControllerBase
    {
        private readonly ISubjectService _service = service;
        private readonly IDistributedCache _cache = cache;
        private readonly AppDbContext _context = context;
        private readonly IDepartmentService _departmentService = departmentService;
        private readonly ICollegeService _collegeService = collegeService;
        private readonly IBatchService _batchService = batchService;
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
            var dtos = list.Select(s => new SubjectDto(s.Id, s.Name, s.Code, s.CollegeId, s.DepartmentId, s.BatchId));

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

            return Ok(new SubjectDto(s.Id, s.Name, s.Code, s.CollegeId, s.DepartmentId, s.BatchId));
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
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
                CollegeId = collegeId,
                DepartmentId = department.Id,
                BatchId = batchId
            };
            var result = await _service.CreateSubjectAsync(entity);

            // Invalidate relevant batch cache
            if (batchId.HasValue)
                await _cache.RemoveAsync($"{CachePrefix}{batchId}");

            return Ok(new SubjectDto(result.Id, result.Name, result.Code, result.CollegeId, result.DepartmentId, result.BatchId));
        }

        [HttpPut("{code}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Update(string code, UpdateSubjectDto dto)
        {
            var entity = await _service.GetSubjectByCodeAsync(code);
            if (entity == null) return NotFound($"Subject with code '{code}' not found.");
            await _service.UpdateSubjectDetailsAsync(entity.Id, dto);
            return NoContent();
        }

        [HttpDelete("{code}")]
        [Authorize(Roles = "Admin")]
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

        [HttpPut("assign-doctor")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> AssignDoctor(string subjectId, string doctorId)
        {
            if (!Ulid.TryParse(subjectId, out var sId)) return BadRequest("Invalid Subject ID.");
            if (!Ulid.TryParse(doctorId, out var dId)) return BadRequest("Invalid Doctor ID.");
            await _service.AssignSubjectToDoctorAsync(sId, dId);
            return NoContent();
        }

        [HttpPut("assign-assistant")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> AssignAssistant(string subjectId, string assistantId)
        {
            if (!Ulid.TryParse(subjectId, out var sId)) return BadRequest("Invalid Subject ID.");
            if (!Ulid.TryParse(assistantId, out var aId)) return BadRequest("Invalid Assistant ID.");
            await _service.AssignSubjectToAssistantAsync(sId, aId);
            return NoContent();
        }
    }
}
