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
    public class DoctorsController(
        IDoctorService service,
        IAuthService authService,
        IDepartmentService departmentService,
        AppDbContext context) : ControllerBase
    {
        private readonly IDoctorService _service = service;
        private readonly IAuthService _authService = authService;
        private readonly IDepartmentService _departmentService = departmentService;
        private readonly AppDbContext _context = context;

        // ── GET /api/doctors/search?q= ────────────────────────────────────────
        [HttpGet("search")]
        public async Task<IActionResult> Search([FromQuery] string? q)
        {
            if (string.IsNullOrWhiteSpace(q))
                return BadRequest("Search query 'q' is required.");

            var pattern = $"%{q}%";
            var results = await _context.Doctors
                .AsNoTracking()
                .Where(d =>
                    EF.Functions.ILike(d.FullName, pattern) ||
                    EF.Functions.ILike(d.Code, pattern) ||
                    EF.Functions.ILike(d.Email, pattern))
                .OrderBy(d => d.FullName)
                .Take(20)
                .Select(d => new { d.Id, d.Code, d.FullName, d.Email, d.Phone, d.UniversityStaffId, d.DepartmentId })
                .ToListAsync();

            return Ok(results);
        }

        // ── GET /api/doctors/filter ───────────────────────────────────────────
        /// <summary>
        /// Filtered, paginated list of doctors with enriched department/college info.
        /// All query params are optional and composable.
        /// Example: GET /api/doctors/filter?departmentId=X&isActive=true&page=1&size=20
        /// </summary>
        [HttpGet("filter")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<IActionResult> Filter([FromQuery] DoctorFilterDto f)
        {
            var page = Math.Max(1, f.Page);
            var size = Math.Clamp(f.Size, 1, 100);

            var query = _context.Doctors
                .AsNoTracking()
                .Include(d => d.SystemUser)
                .Include(d => d.Department)
                    .ThenInclude(dep => dep.College)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(f.DepartmentId) && Ulid.TryParse(f.DepartmentId, out var deptId))
                query = query.Where(d => d.DepartmentId == deptId);

            if (!string.IsNullOrWhiteSpace(f.CollegeId) && Ulid.TryParse(f.CollegeId, out var collegeId))
                query = query.Where(d => d.Department.CollegeId == collegeId);

            if (f.IsActive.HasValue)
                query = query.Where(d => d.SystemUser.IsActive == f.IsActive.Value);

            if (!string.IsNullOrWhiteSpace(f.Search))
            {
                var pattern = $"%{f.Search.Trim()}%";
                query = query.Where(d =>
                    EF.Functions.ILike(d.FullName, pattern) ||
                    EF.Functions.ILike(d.UniversityStaffId, pattern) ||
                    EF.Functions.ILike(d.Email, pattern) ||
                    EF.Functions.ILike(d.Code, pattern));
            }

            var total = await query.CountAsync();

            var data = await query
                .OrderBy(d => d.FullName)
                .Skip((page - 1) * size)
                .Take(size)
                .Select(d => new DoctorDetailDto
                {
                    Id                = d.Id,
                    Code              = d.Code,
                    FullName          = d.FullName,
                    Email             = d.Email,
                    UniversityEmail   = d.SystemUser != null ? d.SystemUser.UniversityEmail : "",
                    Phone             = d.Phone,
                    UniversityStaffId = d.UniversityStaffId,
                    DepartmentId      = d.DepartmentId,
                    DepartmentName    = d.Department != null ? d.Department.Name : "",
                    CollegeId         = d.Department != null ? d.Department.CollegeId : Ulid.Empty,
                    CollegeName       = d.Department != null && d.Department.College != null
                                            ? d.Department.College.Name : ""
                })
                .ToListAsync();

            return Ok(new PagedResult<DoctorDetailDto>
            {
                Data       = data,
                TotalCount = total,
                Page       = page,
                Size       = size
            });
        }

        // ── GET /api/doctors ──────────────────────────────────────────────────
        [HttpGet]
        public async Task<ActionResult<IEnumerable<DoctorDto>>> GetAll([FromQuery] int page = 1, [FromQuery] int size = 10)
        {
            var list = await _service.GetPagedDoctorsAsync(page, size);
            return Ok(list.Select(d => new DoctorDto
            {
                Id                = d.Id,
                Code              = d.Code,
                FullName          = d.FullName,
                Email             = d.Email,
                Phone             = d.Phone,
                UniversityStaffId = d.UniversityStaffId,
                UniversityEmail   = d.SystemUser?.UniversityEmail ?? "N/A",
                DepartmentId      = d.DepartmentId
            }));
        }

        // ── GET /api/doctors/{code} ───────────────────────────────────────────
        [HttpGet("{code}")]
        public async Task<ActionResult<DoctorDto>> GetByCode(string code)
        {
            var d = await _service.GetDoctorByCodeAsync(code);
            if (d == null) return NotFound($"Doctor with code '{code}' not found.");

            return Ok(new DoctorDto
            {
                Id                = d.Id,
                Code              = d.Code,
                FullName          = d.FullName,
                Email             = d.Email,
                Phone             = d.Phone,
                UniversityStaffId = d.UniversityStaffId,
                UniversityEmail   = d.SystemUser?.UniversityEmail ?? "N/A",
                DepartmentId      = d.DepartmentId
            });
        }

        // ── PATCH /api/doctors/{id} ───────────────────────────────────────────
        /// <summary>
        /// Partial update — only the non-null fields you send will be changed.
        /// </summary>
        [HttpPatch("{id}")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<IActionResult> Patch(string id, [FromBody] PatchDoctorDto dto)
        {
            if (!Ulid.TryParse(id, out var doctorId))
                return BadRequest("Invalid doctor ID format.");

            var doctor = await _context.Doctors
                .FirstOrDefaultAsync(d => d.Id == doctorId);

            if (doctor == null) return NotFound($"Doctor with ID '{id}' not found.");

            if (dto.FullName  != null) doctor.FullName = dto.FullName;
            if (dto.Phone     != null) doctor.Phone    = dto.Phone;

            if (dto.DepartmentCode != null)
            {
                var dept = await _context.Departments.FirstOrDefaultAsync(d => d.Code == dto.DepartmentCode);
                if (dept == null) return NotFound($"Department with code '{dto.DepartmentCode}' not found.");
                doctor.DepartmentId = dept.Id;
            }

            await _context.SaveChangesAsync();
            return NoContent();
        }

        // ── POST /api/doctors ─────────────────────────────────────────────────
        [HttpPost]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<ActionResult<DoctorDto>> Create(CreateDoctorDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.DepartmentCode))
                return BadRequest("DepartmentCode is required.");
            var department = await _departmentService.GetDepartmentByCodeAsync(dto.DepartmentCode);
            if (department == null)
                return NotFound($"Department with code '{dto.DepartmentCode}' not found.");

            var registerDto = new RegisterDoctorDto
            {
                FullName       = dto.FullName,
                Phone          = dto.Phone,
                NationalId     = dto.NationalId,
                DepartmentCode = department.Code
            };

            var creatorId = Ulid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
            var authResponse = await _authService.RegisterDoctorAsync(registerDto, creatorId);

            var doctor = await _service.GetDoctorByUniversityEmailAsync(authResponse.UniversityEmail!);
            if (doctor == null) return BadRequest("Failed to retrieve created doctor.");

            return Ok(new DoctorDto
            {
                Id                = doctor.Id,
                Code              = doctor.Code,
                FullName          = doctor.FullName,
                Email             = doctor.Email,
                Phone             = doctor.Phone,
                UniversityStaffId = doctor.UniversityStaffId,
                UniversityEmail   = doctor.SystemUser?.UniversityEmail ?? "N/A",
                DepartmentId      = doctor.DepartmentId
            });
        }

        // ── PUT /api/doctors/{code} ───────────────────────────────────────────
        [HttpPut("{code}")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<IActionResult> Update(string code, UpdateDoctorDto dto)
        {
            var entity = await _service.GetDoctorByCodeAsync(code);
            if (entity == null) return NotFound($"Doctor with code '{code}' not found.");
            try
            {
                await _service.UpdateDoctorDetailsAsync(entity.Id, dto);
                return NoContent();
            }
            catch (Exception ex) { return BadRequest(ex.Message); }
        }

        // ── DELETE /api/doctors/{code} ────────────────────────────────────────
        [HttpDelete("{code}")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<IActionResult> Delete(string code)
        {
            var entity = await _service.GetDoctorByCodeAsync(code);
            if (entity == null) return NotFound($"Doctor with code '{code}' not found.");
            try
            {
                await _service.DeleteDoctorAsync(entity.Id);
                return NoContent();
            }
            catch (Exception ex) { return BadRequest(ex.Message); }
        }

        // ── GET /api/doctors/{code}/subjects ──────────────────────────────────
        [HttpGet("{code}/subjects")]
        public async Task<ActionResult<IEnumerable<SubjectDto>>> GetSubjects(string code)
        {
            var entity = await _service.GetDoctorByCodeAsync(code);
            if (entity == null) return NotFound($"Doctor with code '{code}' not found.");
            var list = await _service.GetDoctorSubjectsAsync(entity.Id);
            return Ok(list.Select(s => new SubjectDto(
                s.Id, s.Name, s.Code, s.CreditHours,
                s.CollegeId, s.College?.Name,
                s.DepartmentId, s.Department?.Name ?? string.Empty,
                s.BatchId, s.Batch?.Name)));
        }

        // ── GET /api/doctors/by-offering/{offeringId} ────────────────────────
        /// <summary>
        /// Returns the doctor assigned to a specific subject offering.
        /// Accessible by Admin and Doctor (own offerings).
        /// </summary>
        [HttpGet("by-offering/{offeringId}")]
        [Authorize(Roles = "Admin,Doctor,SuperAdmin")]
        public async Task<IActionResult> GetByOffering(string offeringId)
        {
            if (!Ulid.TryParse(offeringId, out var oId))
                return BadRequest("Invalid Offering ID.");

            var doctor = await _context.SubjectOfferings
                .AsNoTracking()
                .Where(o => o.Id == oId)
                .Select(o => new DoctorSummaryDto
                {
                    Id             = o.Doctor.Id.ToString(),
                    Code           = o.Doctor.Code,
                    FullName       = o.Doctor.FullName,
                    Email          = o.Doctor.Email,
                    DepartmentId   = o.Doctor.DepartmentId.ToString(),
                    DepartmentName = o.Doctor.Department != null ? o.Doctor.Department.Name : "",
                    CollegeName    = o.Doctor.Department != null && o.Doctor.Department.College != null
                                        ? o.Doctor.Department.College.Name : "",
                })
                .FirstOrDefaultAsync();

            if (doctor == null) return NotFound("Offering not found.");
            return Ok(doctor);
        }

        // ── GET /api/doctors/by-subject/{subjectId} ───────────────────────────
        /// <summary>
        /// Returns all doctors assigned to teach a subject (via SubjectDoctor junction).
        /// Paginated: ?page=1&size=20
        /// </summary>
        [HttpGet("by-subject/{subjectId}")]
        [Authorize(Roles = "Admin,Doctor,SuperAdmin")]
        public async Task<IActionResult> GetBySubject(
            string subjectId,
            [FromQuery] int page = 1,
            [FromQuery] int size = 20)
        {
            if (!Ulid.TryParse(subjectId, out var sId))
                return BadRequest("Invalid Subject ID.");

            page = Math.Max(1, page);
            size = Math.Clamp(size, 1, 100);

            var query = _context.SubjectDoctors
                .AsNoTracking()
                .Where(sd => sd.SubjectId == sId)
                .Select(sd => sd.Doctor);

            var total = await query.CountAsync();

            var data = await query
                .OrderBy(d => d.FullName)
                .Skip((page - 1) * size)
                .Take(size)
                .Select(d => new DoctorSummaryDto
                {
                    Id             = d.Id.ToString(),
                    Code           = d.Code,
                    FullName       = d.FullName,
                    Email          = d.Email,
                    DepartmentId   = d.DepartmentId.ToString(),
                    DepartmentName = d.Department != null ? d.Department.Name : "",
                    CollegeName    = d.Department != null && d.Department.College != null
                                        ? d.Department.College.Name : "",
                })
                .ToListAsync();

            return Ok(new PagedResult<DoctorSummaryDto>
            {
                Data       = data,
                TotalCount = total,
                Page       = page,
                Size       = size,
            });
        }

        // ── POST /api/doctors/bulk-upload ─────────────────────────────────────
        [HttpPost("bulk-upload")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> BulkUpload(IFormFile file, [FromServices] IFileService fileService, [FromServices] IBackgroundJobClient jobClient)
        {
            if (file == null || file.Length == 0) return BadRequest("File is empty");
            var userIdClaim = User.FindFirst("nameid");
            if (userIdClaim == null) return Unauthorized("Invalid token claims");
            var userId = Ulid.Parse(userIdClaim.Value);
            using var stream = file.OpenReadStream();
            var fileId = await fileService.UploadFileStreamAsync(userId, stream, file.FileName, file.ContentType, file.Length);
            jobClient.Enqueue<IBulkUploadJob>(x => x.ProcessDoctorUpload(fileId, userId));
            return Accepted(new { JobId = fileId, Message = "File accepted for processing" });
        }
    }
}
