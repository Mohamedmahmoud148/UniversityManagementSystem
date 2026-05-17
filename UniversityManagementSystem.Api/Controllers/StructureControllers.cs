using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using NUlid;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using UniversityManagementSystem.Core.DTOs;
using UniversityManagementSystem.Core.Entities;
using UniversityManagementSystem.Core.Interfaces;
using UniversityManagementSystem.Infrastructure.Data;

namespace UniversityManagementSystem.Api.Controllers
{
    // ─────────────────────────────────────────────────────────
    // DTOs for GET /api/university/full-structure
    // ─────────────────────────────────────────────────────────

    public record FullStudentDto(string Id, string Code, string FullName, string UniversityStudentId);
    public record FullGroupDto(string Id, string Code, string Name, IReadOnlyList<FullStudentDto> Students);
    public record FullBatchDto(string Id, string Code, string Name, IReadOnlyList<FullGroupDto> Groups);
    public record FullSubjectDto(string Id, string Code, string Name, int CreditHours);
    public record FullDoctorDto(string Id, string Code, string FullName, string Email);
    public record FullDepartmentDto(
        string Id, string Code, string Name,
        IReadOnlyList<FullSubjectDto> Subjects,
        IReadOnlyList<FullBatchDto> Batches,
        IReadOnlyList<FullDoctorDto> Doctors);
    public record FullCollegeDto(string Id, string Code, string Name, IReadOnlyList<FullDepartmentDto> Departments);
    public record FullUniversityDto(string Id, string Code, string Name, IReadOnlyList<FullCollegeDto> Colleges);

    // ─────────────────────────────────────────────────────────
    // University Controller
    // ─────────────────────────────────────────────────────────

    [Authorize(Roles = "Admin,Student,Doctor")]
    [ApiController]
    [Route("api/[controller]")]
    public class UniversityController : ControllerBase
    {
        private const string StructureCacheKey = "university:full-structure";
        private static readonly DistributedCacheEntryOptions _structureCacheOpts = new()
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(60)
        };

        private readonly IUniversityService _service;
        private readonly AppDbContext _context;
        private readonly ILogger<UniversityController> _logger;
        private readonly IDistributedCache _cache;

        public UniversityController(
            IUniversityService service,
            AppDbContext context,
            ILogger<UniversityController> logger,
            IDistributedCache cache)
        {
            _service = service;
            _context = context;
            _logger = logger;
            _cache = cache;
        }

        [HttpGet("structure")]
        public async Task<ActionResult<IEnumerable<UniversityDto>>> GetStructure()
        {
            var universities = await _service.GetAllUniversitiesAsync();
            // ✅ FIX: UniversityDto now includes Code (was missing before)
            return Ok(universities.Select(u => new UniversityDto(u.Id, u.Name, u.Code)));
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<UniversityDto>> Create(CreateUniversityDto dto)
        {
            var codeUpper = dto.Code.ToUpper();
            var existing = await _service.GetUniversityByCodeAsync(codeUpper);
            if (existing != null)
                return Conflict($"A University with code '{codeUpper}' already exists.");

            var entity = new University { Name = dto.Name, Code = codeUpper };
            var result = await _service.CreateUniversityAsync(entity);
            return Ok(new UniversityDto(result.Id, result.Name, result.Code));
        }

        // ── LEGACY (keep for backward compat): PUT/DELETE by internal ULID ────
        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        [ApiExplorerSettings(GroupName = "legacy")]
        public async Task<IActionResult> UpdateById(string id, CreateUniversityDto dto)
        {
            if (!Ulid.TryParse(id, out var uId)) return BadRequest("Invalid University ID.");
            var entity = await _service.GetUniversityByIdAsync(uId);
            if (entity == null) return NotFound();
            entity.Name = dto.Name;
            await _service.UpdateUniversityAsync(entity);
            return NoContent();
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        [ApiExplorerSettings(GroupName = "legacy")]
        public async Task<IActionResult> DeleteById(string id)
        {
            if (!Ulid.TryParse(id, out var uId)) return BadRequest("Invalid University ID.");
            await _service.DeleteUniversityAsync(uId);
            return NoContent();
        }

        // ── NEW (preferred): Admin routes using public Code ───────────────────
        /// <summary>
        /// [PREFERRED] Update a University by its public Code.
        /// Admin/frontend MUST use this route.
        /// </summary>
        [HttpPut("by-code/{code}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateByCode(string code, CreateUniversityDto dto)
        {
            var entity = await _service.GetUniversityByCodeAsync(code);
            if (entity == null) return NotFound($"University with code '{code}' not found.");

            entity.Name = dto.Name;
            // Update Code if a new one is explicitly provided and differs
            if (!string.IsNullOrWhiteSpace(dto.Code) && dto.Code.ToUpper() != entity.Code)
            {
                var conflict = await _service.GetUniversityByCodeAsync(dto.Code.ToUpper());
                if (conflict != null)
                    return Conflict($"A University with code '{dto.Code.ToUpper()}' already exists.");
                entity.Code = dto.Code.ToUpper();
            }

            await _service.UpdateUniversityAsync(entity);
            await _cache.RemoveAsync(StructureCacheKey); // Invalidate full-structure cache
            return NoContent();
        }

        /// <summary>
        /// [PREFERRED] Delete a University by its public Code.
        /// Admin/frontend MUST use this route.
        /// </summary>
        [HttpDelete("by-code/{code}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteByCode(string code)
        {
            var entity = await _service.GetUniversityByCodeAsync(code);
            if (entity == null) return NotFound($"University with code '{code}' not found.");

            await _service.DeleteUniversityAsync(entity.Id);
            await _cache.RemoveAsync(StructureCacheKey);
            return NoContent();
        }

        // ── GET /api/university/full-structure ──────────────────────────────
        [HttpGet("full-structure")]
        public async Task<IActionResult> GetFullStructure()
        {
            // Hit Redis first — this query is expensive (6-table join)
            var cached = await _cache.GetStringAsync(StructureCacheKey);
            if (!string.IsNullOrEmpty(cached))
            {
                _logger.LogDebug("full-structure: served from cache");
                return Content(cached, "application/json");
            }

            var list = await _context.Universities
                .AsNoTracking()
                .Select(u => new FullUniversityDto(
                    u.Id.ToString(), u.Code, u.Name,
                    u.Colleges
                        .Select(col => new FullCollegeDto(
                            col.Id.ToString(), col.Code, col.Name,
                            col.Departments
                                .Select(dep => new FullDepartmentDto(
                                    dep.Id.ToString(), dep.Code, dep.Name,
                                    dep.Subjects
                                        .Select(s => new FullSubjectDto(s.Id.ToString(), s.Code, s.Name, s.CreditHours))
                                        .ToList(),
                                    dep.Batches
                                        .Select(b => new FullBatchDto(
                                            b.Id.ToString(), b.Code, b.Name,
                                            b.Groups
                                                .Select(g => new FullGroupDto(
                                                    g.Id.ToString(), g.Code, g.Name,
                                                    g.Students
                                                        .Select(st => new FullStudentDto(st.Id.ToString(), st.Code, st.FullName, st.UniversityStudentId))
                                                        .ToList()))
                                                .ToList()))
                                        .ToList(),
                                    dep.Doctors
                                        .Select(d => new FullDoctorDto(d.Id.ToString(), d.Code, d.FullName, d.Email))
                                        .ToList()))
                                .ToList()))
                        .ToList()))
                .ToListAsync();

            if (list.Count == 0)
            {
                _logger.LogWarning("full-structure: no universities found.");
                return NotFound("No universities found.");
            }

            var result = list.Count == 1 ? (object)list[0] : list;
            var json = JsonSerializer.Serialize(result);

            // Store in Redis for 60 minutes
            await _cache.SetStringAsync(StructureCacheKey, json, _structureCacheOpts);
            _logger.LogDebug("full-structure: cached for 60 min");

            return Content(json, "application/json");
        }
    }

    // ─────────────────────────────────────────────────────────
    // Colleges Controller
    // ─────────────────────────────────────────────────────────

    [Authorize(Roles = "Admin,Student,Doctor")]
    [ApiController]
    [Route("api/[controller]")]
    public class CollegesController(ICollegeService service, IUniversityService universityService, AppDbContext context) : ControllerBase
    {
        private readonly ICollegeService _service = service;
        private readonly IUniversityService _universityService = universityService;
        private readonly AppDbContext _context = context;

        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            if (page < 1) page = 1;
            if (pageSize is < 1 or > 100) pageSize = 10;

            var total = await _context.Colleges.CountAsync();
            var list = await _context.Colleges
                .AsNoTracking()
                .OrderBy(c => c.Name)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(c => new CollegeDto(c.Id, c.Name, c.Code, c.UniversityId))
                .ToListAsync();

            return Ok(new { Page = page, PageSize = pageSize, Total = total, Data = list });
        }

        [HttpGet("by-code/{code}")]
        public async Task<ActionResult<CollegeDto>> GetByCode(string code)
        {
            var c = await _service.GetCollegeByCodeAsync(code);
            if (c == null) return NotFound();
            return Ok(new CollegeDto(c.Id, c.Name, c.Code, c.UniversityId));
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<CollegeDto>> Create(CreateCollegeDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Code))
                return BadRequest("College code is required.");

            // ✅ FIX: Resolve UniversityCode → UniversityId internally
            if (string.IsNullOrWhiteSpace(dto.UniversityCode))
                return BadRequest("UniversityCode is required.");

            var university = await _universityService.GetUniversityByCodeAsync(dto.UniversityCode);
            if (university == null)
                return NotFound($"University with code '{dto.UniversityCode}' not found.");

            var codeUpper = dto.Code.ToUpper();
            var existing = await _service.GetCollegeByCodeAsync(codeUpper);
            if (existing != null)
                return Conflict($"A College with code '{codeUpper}' already exists.");

            var entity = new College { Name = dto.Name, Code = codeUpper, UniversityId = university.Id };
            var result = await _service.CreateCollegeAsync(entity);
            return Ok(new CollegeDto(result.Id, result.Name, result.Code, result.UniversityId));
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Update(string id, CreateCollegeDto dto)
        {
            if (!Ulid.TryParse(id, out var collegeId)) return BadRequest("Invalid College ID.");

            var entity = await _service.GetCollegeByIdAsync(collegeId);
            if (entity == null) return NotFound($"College with ID '{id}' not found.");

            entity.Name = dto.Name;

            if (!string.IsNullOrWhiteSpace(dto.UniversityCode))
            {
                var university = await _universityService.GetUniversityByCodeAsync(dto.UniversityCode);
                if (university == null)
                    return NotFound($"University with code '{dto.UniversityCode}' not found.");
                entity.UniversityId = university.Id;
            }

            if (!string.IsNullOrWhiteSpace(dto.Code) && dto.Code.ToUpper() != entity.Code)
            {
                var codeConflict = await _service.GetCollegeByCodeAsync(dto.Code.ToUpper());
                if (codeConflict != null)
                    return Conflict($"A College with code '{dto.Code.ToUpper()}' already exists.");
                entity.Code = dto.Code.ToUpper();
            }

            await _service.UpdateCollegeAsync(entity);
            return NoContent();
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(string id)
        {
            if (!Ulid.TryParse(id, out var collegeId)) return BadRequest("Invalid College ID.");
            var entity = await _service.GetCollegeByIdAsync(collegeId);
            if (entity == null) return NotFound($"College with ID '{id}' not found.");
            await _service.DeleteCollegeAsync(entity.Id);
            return NoContent();
        }
    }

    // ─────────────────────────────────────────────────────────
    // Departments Controller
    // ─────────────────────────────────────────────────────────

    [Authorize(Roles = "Admin,Student,Doctor")]
    [ApiController]
    [Route("api/[controller]")]
    public class DepartmentsController(IDepartmentService service, ICollegeService collegeService, AppDbContext context) : ControllerBase
    {
        private readonly IDepartmentService _service = service;
        private readonly ICollegeService _collegeService = collegeService;
        private readonly AppDbContext _context = context;

        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            if (page < 1) page = 1;
            if (pageSize is < 1 or > 100) pageSize = 10;

            var total = await _context.Departments.CountAsync();
            var list = await _context.Departments
                .AsNoTracking()
                .OrderBy(d => d.Name)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(d => new DepartmentDto(d.Id, d.Name, d.Code, d.CollegeId))
                .ToListAsync();

            return Ok(new { Page = page, PageSize = pageSize, Total = total, Data = list });
        }

        [HttpGet("by-college/{collegeId}")]
        public async Task<ActionResult<IEnumerable<DepartmentDto>>> GetByCollege(string collegeId)
        {
            if (!Ulid.TryParse(collegeId, out var cId)) return BadRequest("Invalid College ID.");
            var list = await _service.GetDepartmentsByCollegeIdAsync(cId);
            return Ok(list.Select(d => new DepartmentDto(d.Id, d.Name, d.Code, d.CollegeId)));
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<DepartmentDto>> Create(CreateDepartmentDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.CollegeCode))
                return BadRequest("CollegeCode is required.");

            var college = await _collegeService.GetCollegeByCodeAsync(dto.CollegeCode);
            if (college == null)
                return NotFound($"College with code '{dto.CollegeCode}' not found.");

            var codeUpper = dto.Code.ToUpper();
            var existing = await _service.GetDepartmentByCodeAsync(codeUpper);
            if (existing != null)
                return Conflict($"A Department with code '{codeUpper}' already exists.");

            var entity = new Department { Name = dto.Name, Code = codeUpper, CollegeId = college.Id };
            var result = await _service.CreateDepartmentAsync(entity);

            // If academicYearId provided, link the new department to that year
            if (!string.IsNullOrWhiteSpace(dto.AcademicYearId) && Ulid.TryParse(dto.AcademicYearId, out var yearId))
            {
                var yearExists = await _context.Set<AcademicYear>().AnyAsync(y => y.Id == yearId);
                if (yearExists)
                {
                    _context.AcademicYearDepartments.Add(new AcademicYearDepartment
                    {
                        AcademicYearId = yearId,
                        DepartmentId   = result.Id,
                        IsActive       = true
                    });
                    await _context.SaveChangesAsync();
                }
            }

            return Ok(new DepartmentDto(result.Id, result.Name, result.Code, result.CollegeId));
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Update(string id, CreateDepartmentDto dto)
        {
            if (!Ulid.TryParse(id, out var departmentId)) return BadRequest("Invalid Department ID.");

            if (string.IsNullOrWhiteSpace(dto.CollegeCode))
                return BadRequest("CollegeCode is required.");

            var college = await _collegeService.GetCollegeByCodeAsync(dto.CollegeCode);
            if (college == null)
                return NotFound($"College with code '{dto.CollegeCode}' not found.");

            var entity = await _service.GetDepartmentByIdAsync(departmentId);
            if (entity == null) return NotFound($"Department with ID '{id}' not found.");
            entity.Name = dto.Name;
            entity.CollegeId = college.Id;

            if (!string.IsNullOrWhiteSpace(dto.Code) && dto.Code.ToUpper() != entity.Code)
            {
                var codeConflict = await _service.GetDepartmentByCodeAsync(dto.Code.ToUpper());
                if (codeConflict != null && codeConflict.Id != entity.Id)
                    return Conflict($"A Department with code '{dto.Code.ToUpper()}' already exists.");
                entity.Code = dto.Code.ToUpper();
            }

            await _service.UpdateDepartmentAsync(entity);
            return NoContent();
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(string id)
        {
            if (!Ulid.TryParse(id, out var departmentId)) return BadRequest("Invalid Department ID.");

            var entity = await _service.GetDepartmentByIdAsync(departmentId);
            if (entity == null) return NotFound($"Department with ID '{id}' not found.");
            await _service.DeleteDepartmentAsync(departmentId);
            return NoContent();
        }
    }

    // ─────────────────────────────────────────────────────────
    // Batches Controller
    // ─────────────────────────────────────────────────────────

    [Authorize(Roles = "Admin,Student,Doctor")]
    [ApiController]
    [Route("api/[controller]")]
    public class BatchesController(IBatchService service, IDepartmentService departmentService, AppDbContext context) : ControllerBase
    {
        private readonly IBatchService _service = service;
        private readonly IDepartmentService _departmentService = departmentService;
        private readonly AppDbContext _context = context;

        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            if (page < 1) page = 1;
            if (pageSize is < 1 or > 100) pageSize = 10;

            var total = await _context.Batches.CountAsync();
            var list = await _context.Batches
                .AsNoTracking()
                .OrderBy(b => b.Name)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(b => new BatchDto(b.Id, b.Name, b.Code, b.DepartmentId))
                .ToListAsync();

            return Ok(new { Page = page, PageSize = pageSize, Total = total, Data = list });
        }

        [HttpGet("by-department/{departmentId}")]
        public async Task<ActionResult<IEnumerable<BatchDto>>> GetByDepartment(string departmentId)
        {
            if (!Ulid.TryParse(departmentId, out var dId)) return BadRequest("Invalid Department ID.");
            var list = await _service.GetBatchesByDepartmentIdAsync(dId);
            return Ok(list.Select(b => new BatchDto(b.Id, b.Name, b.Code, b.DepartmentId)));
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<BatchDto>> Create(CreateBatchDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.DepartmentCode))
                return BadRequest("DepartmentCode is required.");

            var department = await _departmentService.GetDepartmentByCodeAsync(dto.DepartmentCode);
            if (department == null)
                return NotFound($"Department with code '{dto.DepartmentCode}' not found.");

            var codeUpper = dto.Code.ToUpper();
            var existing = await _service.GetBatchByCodeAsync(codeUpper);
            if (existing != null)
                return Conflict($"A Batch with code '{codeUpper}' already exists.");

            var entity = new Batch { Name = dto.Name, Code = codeUpper, DepartmentId = department.Id };
            var result = await _service.CreateBatchAsync(entity);
            return Ok(new BatchDto(result.Id, result.Name, result.Code, result.DepartmentId));
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Update(string id, CreateBatchDto dto)
        {
            if (!Ulid.TryParse(id, out var batchId)) return BadRequest("Invalid Batch ID.");

            if (string.IsNullOrWhiteSpace(dto.DepartmentCode))
                return BadRequest("DepartmentCode is required.");

            var department = await _departmentService.GetDepartmentByCodeAsync(dto.DepartmentCode);
            if (department == null)
                return NotFound($"Department with code '{dto.DepartmentCode}' not found.");

            var entity = await _service.GetBatchByIdAsync(batchId);
            if (entity == null) return NotFound($"Batch with ID '{id}' not found.");
            entity.Name = dto.Name;
            entity.DepartmentId = department.Id;

            if (!string.IsNullOrWhiteSpace(dto.Code) && dto.Code.ToUpper() != entity.Code)
            {
                var codeConflict = await _service.GetBatchByCodeAsync(dto.Code.ToUpper());
                if (codeConflict != null)
                    return Conflict($"A Batch with code '{dto.Code.ToUpper()}' already exists.");
                entity.Code = dto.Code.ToUpper();
            }

            await _service.UpdateBatchAsync(entity);
            return NoContent();
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(string id)
        {
            if (!Ulid.TryParse(id, out var batchId)) return BadRequest("Invalid Batch ID.");
            var entity = await _service.GetBatchByIdAsync(batchId);
            if (entity == null) return NotFound($"Batch with ID '{id}' not found.");
            await _service.DeleteBatchAsync(entity.Id);
            return NoContent();
        }
    }

    // ─────────────────────────────────────────────────────────
    // Groups Controller
    // ─────────────────────────────────────────────────────────

    [Authorize(Roles = "Admin,Student,Doctor")]
    [ApiController]
    [Route("api/[controller]")]
    public class GroupsController(IGroupService service, IBatchService batchService, AppDbContext context) : ControllerBase
    {
        private readonly IGroupService _service = service;
        private readonly IBatchService _batchService = batchService;
        private readonly AppDbContext _context = context;

        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            if (page < 1) page = 1;
            if (pageSize is < 1 or > 100) pageSize = 10;

            var total = await _context.Groups.CountAsync();
            var list = await _context.Groups
                .AsNoTracking()
                .OrderBy(g => g.Name)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                // ✅ FIX: GroupDto now includes Code field
                .Select(g => new GroupDto(g.Id, g.Name, g.Code, g.BatchId))
                .ToListAsync();

            return Ok(new { Page = page, PageSize = pageSize, Total = total, Data = list });
        }

        [HttpGet("by-batch/{batchId}")]
        public async Task<ActionResult<IEnumerable<GroupDto>>> GetByBatch(string batchId)
        {
            if (!Ulid.TryParse(batchId, out var bId)) return BadRequest("Invalid Batch ID.");
            var list = await _service.GetGroupsByBatchIdAsync(bId);
            // ✅ FIX: GroupDto now includes Code field
            return Ok(list.Select(g => new GroupDto(g.Id, g.Name, g.Code, g.BatchId)));
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<GroupDto>> Create(CreateGroupDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.BatchCode))
                return BadRequest("BatchCode is required.");

            var batch = await _batchService.GetBatchByCodeAsync(dto.BatchCode);
            if (batch == null)
                return NotFound($"Batch with code '{dto.BatchCode}' not found.");

            var codeUpper = dto.Code.ToUpper();
            var existing = await _service.GetGroupByCodeAsync(codeUpper);
            if (existing != null)
                return Conflict($"A Group with code '{codeUpper}' already exists.");

            var entity = new Group { Name = dto.Name, Code = codeUpper, BatchId = batch.Id };
            var result = await _service.CreateGroupAsync(entity);
            // ✅ FIX: GroupDto now includes Code field
            return Ok(new GroupDto(result.Id, result.Name, result.Code, result.BatchId));
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Update(string id, CreateGroupDto dto)
        {
            if (!Ulid.TryParse(id, out var groupId)) return BadRequest("Invalid Group ID.");

            if (string.IsNullOrWhiteSpace(dto.BatchCode))
                return BadRequest("BatchCode is required.");

            var batch = await _batchService.GetBatchByCodeAsync(dto.BatchCode);
            if (batch == null)
                return NotFound($"Batch with code '{dto.BatchCode}' not found.");

            var entity = await _service.GetGroupByIdAsync(groupId);
            if (entity == null) return NotFound($"Group with ID '{id}' not found.");
            entity.Name = dto.Name;
            entity.BatchId = batch.Id;

            if (!string.IsNullOrWhiteSpace(dto.Code) && dto.Code.ToUpper() != entity.Code)
            {
                var codeConflict = await _service.GetGroupByCodeAsync(dto.Code.ToUpper());
                if (codeConflict != null)
                    return Conflict($"A Group with code '{dto.Code.ToUpper()}' already exists.");
                entity.Code = dto.Code.ToUpper();
            }

            await _service.UpdateGroupAsync(entity);
            return NoContent();
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(string id)
        {
            if (!Ulid.TryParse(id, out var groupId)) return BadRequest("Invalid Group ID.");
            var entity = await _service.GetGroupByIdAsync(groupId);
            if (entity == null) return NotFound($"Group with ID '{id}' not found.");
            await _service.DeleteGroupAsync(entity.Id);
            return NoContent();
        }
    }
}
