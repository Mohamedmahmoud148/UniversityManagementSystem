using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NUlid;
using System.Collections.Generic;
using System.Linq;
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
        private readonly IUniversityService _service;
        private readonly AppDbContext _context;
        private readonly ILogger<UniversityController> _logger;

        public UniversityController(
            IUniversityService service,
            AppDbContext context,
            ILogger<UniversityController> logger)
        {
            _service = service;
            _context = context;
            _logger = logger;
        }

        [HttpGet("structure")]
        public async Task<ActionResult<IEnumerable<UniversityDto>>> GetStructure()
        {
            var universities = await _service.GetAllUniversitiesAsync();
            return Ok(universities.Select(u => new UniversityDto(u.Id, u.Name)));
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<UniversityDto>> Create(CreateUniversityDto dto)
        {
            var entity = new University { Name = dto.Name };
            var result = await _service.CreateUniversityAsync(entity);
            return Ok(new UniversityDto(result.Id, result.Name));
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Update(string id, CreateUniversityDto dto)
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
        public async Task<IActionResult> Delete(string id)
        {
            if (!Ulid.TryParse(id, out var uId)) return BadRequest("Invalid University ID.");
            await _service.DeleteUniversityAsync(uId);
            return NoContent();
        }

        // ── GET /api/university/full-structure ──────────────────────────────
        [HttpGet("full-structure")]
        public async Task<IActionResult> GetFullStructure()
        {
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

            return list.Count == 1 ? Ok(list[0]) : Ok(list);
        }
    }

    // ─────────────────────────────────────────────────────────
    // Colleges Controller
    // ─────────────────────────────────────────────────────────

    [Authorize(Roles = "Admin,Student,Doctor")]
    [ApiController]
    [Route("api/[controller]")]
    public class CollegesController(ICollegeService service) : ControllerBase
    {
        private readonly ICollegeService _service = service;

        [HttpGet]
        public async Task<ActionResult<IEnumerable<CollegeDto>>> GetAll()
        {
            var list = await _service.GetAllCollegesAsync();
            return Ok(list.Select(c => new CollegeDto(c.Id, c.Name, c.Code, c.UniversityId)));
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
            var entity = new College { Name = dto.Name, UniversityId = dto.UniversityId };
            var result = await _service.CreateCollegeAsync(entity);
            return Ok(new CollegeDto(result.Id, result.Name, result.Code, result.UniversityId));
        }

        [HttpPut("{code}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Update(string code, CreateCollegeDto dto)
        {
            var entity = await _service.GetCollegeByCodeAsync(code);
            if (entity == null) return NotFound($"College with code '{code}' not found.");
            entity.Name = dto.Name;
            entity.UniversityId = dto.UniversityId;
            await _service.UpdateCollegeAsync(entity);
            return NoContent();
        }

        [HttpDelete("{code}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(string code)
        {
            var entity = await _service.GetCollegeByCodeAsync(code);
            if (entity == null) return NotFound($"College with code '{code}' not found.");
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
    public class DepartmentsController(IDepartmentService service, ICollegeService collegeService) : ControllerBase
    {
        private readonly IDepartmentService _service = service;
        private readonly ICollegeService _collegeService = collegeService;

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

            var entity = new Department { Name = dto.Name, CollegeId = college.Id };
            var result = await _service.CreateDepartmentAsync(entity);
            return Ok(new DepartmentDto(result.Id, result.Name, result.Code, result.CollegeId));
        }

        [HttpPut("{code}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Update(string code, CreateDepartmentDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.CollegeCode))
                return BadRequest("CollegeCode is required.");

            var college = await _collegeService.GetCollegeByCodeAsync(dto.CollegeCode);
            if (college == null)
                return NotFound($"College with code '{dto.CollegeCode}' not found.");

            var entity = await _service.GetDepartmentByCodeAsync(code);
            if (entity == null) return NotFound($"Department with code '{code}' not found.");
            entity.Name = dto.Name;
            entity.CollegeId = college.Id;
            await _service.UpdateDepartmentAsync(entity);
            return NoContent();
        }

        [HttpDelete("{code}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(string code)
        {
            var entity = await _service.GetDepartmentByCodeAsync(code);
            if (entity == null) return NotFound($"Department with code '{code}' not found.");
            await _service.DeleteDepartmentAsync(entity.Id);
            return NoContent();
        }
    }

    // ─────────────────────────────────────────────────────────
    // Batches Controller
    // ─────────────────────────────────────────────────────────

    [Authorize(Roles = "Admin,Student,Doctor")]
    [ApiController]
    [Route("api/[controller]")]
    public class BatchesController(IBatchService service, IDepartmentService departmentService) : ControllerBase
    {
        private readonly IBatchService _service = service;
        private readonly IDepartmentService _departmentService = departmentService;

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

            var entity = new Batch { Name = dto.Name, DepartmentId = department.Id };
            var result = await _service.CreateBatchAsync(entity);
            return Ok(new BatchDto(result.Id, result.Name, result.Code, result.DepartmentId));
        }

        [HttpPut("{code}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Update(string code, CreateBatchDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.DepartmentCode))
                return BadRequest("DepartmentCode is required.");

            var department = await _departmentService.GetDepartmentByCodeAsync(dto.DepartmentCode);
            if (department == null)
                return NotFound($"Department with code '{dto.DepartmentCode}' not found.");

            var entity = await _service.GetBatchByCodeAsync(code);
            if (entity == null) return NotFound($"Batch with code '{code}' not found.");
            entity.Name = dto.Name;
            entity.DepartmentId = department.Id;
            await _service.UpdateBatchAsync(entity);
            return NoContent();
        }

        [HttpDelete("{code}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(string code)
        {
            var entity = await _service.GetBatchByCodeAsync(code);
            if (entity == null) return NotFound($"Batch with code '{code}' not found.");
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
    public class GroupsController(IGroupService service, IBatchService batchService) : ControllerBase
    {
        private readonly IGroupService _service = service;
        private readonly IBatchService _batchService = batchService;

        [HttpGet("by-batch/{batchId}")]
        public async Task<ActionResult<IEnumerable<GroupDto>>> GetByBatch(string batchId)
        {
            if (!Ulid.TryParse(batchId, out var bId)) return BadRequest("Invalid Batch ID.");
            var list = await _service.GetGroupsByBatchIdAsync(bId);
            return Ok(list.Select(g => new GroupDto(g.Id, g.Name, g.BatchId)));
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

            var entity = new Group { Name = dto.Name, BatchId = batch.Id };
            var result = await _service.CreateGroupAsync(entity);
            return Ok(new GroupDto(result.Id, result.Name, result.BatchId));
        }

        [HttpPut("{code}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Update(string code, CreateGroupDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.BatchCode))
                return BadRequest("BatchCode is required.");

            var batch = await _batchService.GetBatchByCodeAsync(dto.BatchCode);
            if (batch == null)
                return NotFound($"Batch with code '{dto.BatchCode}' not found.");

            var entity = await _service.GetGroupByCodeAsync(code);
            if (entity == null) return NotFound($"Group with code '{code}' not found.");
            entity.Name = dto.Name;
            entity.BatchId = batch.Id;
            await _service.UpdateGroupAsync(entity);
            return NoContent();
        }

        [HttpDelete("{code}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(string code)
        {
            var entity = await _service.GetGroupByCodeAsync(code);
            if (entity == null) return NotFound($"Group with code '{code}' not found.");
            await _service.DeleteGroupAsync(entity.Id);
            return NoContent();
        }
    }
}
