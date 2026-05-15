using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NUlid;
using UniversityManagementSystem.Core.DTOs;
using UniversityManagementSystem.Core.Interfaces;
using UniversityManagementSystem.Infrastructure.Data;

namespace UniversityManagementSystem.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SubjectOfferingsController(
        ISubjectOfferingService service,
        IEnrollmentService enrollmentService,
        AppDbContext context) : ControllerBase
    {
        private readonly ISubjectOfferingService _service = service;
        private readonly IEnrollmentService _enrollmentService = enrollmentService;
        private readonly AppDbContext _context = context;

        [HttpGet("by-code/{code}")]
        public async Task<IActionResult> GetByCode(string code)
        {
            var result = await _service.GetByCodeAsync(code);
            if (result == null) return NotFound();
            return Ok(result);
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create([FromBody] CreateSubjectOfferingDto dto)
        {
            var result = await _service.CreateAsync(dto);
            return CreatedAtAction(nameof(GetBySemester), new { semesterId = result.SemesterId }, result);
        }

        [HttpGet("by-semester/{semesterId}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetBySemester(string semesterId)
        {
            if (!Ulid.TryParse(semesterId, out var uid)) return BadRequest("Invalid Semester ID.");
            var result = await _service.GetBySemesterAsync(uid);
            return Ok(result);
        }

        [HttpGet("my-offerings")]
        [Authorize(Roles = "Doctor")]
        public async Task<IActionResult> GetMyOfferings()
        {
            var claim = User.Claims.FirstOrDefault(c => c.Type == "nameid");
            if (claim == null) return Unauthorized("User ID claim not found.");
            if (!Ulid.TryParse(claim.Value, out var userId)) return Unauthorized("Invalid user ID.");
            var result = await _service.GetByDoctorAsync(userId);
            return Ok(result);
        }

        // ── GET /api/subjectofferings/by-department/{departmentId} ───────────
        /// <summary>
        /// Returns all offerings for a department.
        /// Optional: ?batchId=&semesterId=&page=1&size=20
        /// </summary>
        [HttpGet("by-department/{departmentId}")]
        [Authorize(Roles = "Admin,Doctor")]
        public async Task<IActionResult> GetByDepartment(
            string departmentId,
            [FromQuery] string? batchId    = null,
            [FromQuery] string? semesterId = null,
            [FromQuery] int     page       = 1,
            [FromQuery] int     size       = 20)
        {
            if (!Ulid.TryParse(departmentId, out var dId))
                return BadRequest("Invalid Department ID.");

            page = Math.Max(1, page);
            size = Math.Clamp(size, 1, 100);

            var q = _context.SubjectOfferings
                .AsNoTracking()
                .Where(o => o.DepartmentId == dId);

            if (!string.IsNullOrWhiteSpace(batchId) && Ulid.TryParse(batchId, out var bId))
                q = q.Where(o => o.BatchId == bId);

            if (!string.IsNullOrWhiteSpace(semesterId) && Ulid.TryParse(semesterId, out var sId))
                q = q.Where(o => o.SemesterId == sId);

            var total = await q.CountAsync();

            var data = await q
                .OrderBy(o => o.Subject.Name)
                .Skip((page - 1) * size)
                .Take(size)
                .Select(o => new OfferingSummaryDto
                {
                    Id             = o.Id.ToString(),
                    Code           = o.Code,
                    SubjectName    = o.Subject != null ? o.Subject.Name : "",
                    SubjectCode    = o.Subject != null ? o.Subject.Code : "",
                    DoctorName     = o.Doctor != null ? o.Doctor.FullName : "",
                    DoctorId       = o.DoctorId.ToString(),
                    DepartmentName = o.Department != null ? o.Department.Name : "",
                    BatchName      = o.Batch != null ? o.Batch.Name : "",
                    SemesterName   = o.Semester != null ? o.Semester.Name : "",
                    MaxCapacity    = o.MaxCapacity,
                    EnrolledCount  = _context.Enrollments.Count(e => e.SubjectOfferingId == o.Id && e.IsActive && e.DeletedAt == null),
                })
                .ToListAsync();

            return Ok(new PagedResult<OfferingSummaryDto>
            {
                Data = data, TotalCount = total, Page = page, Size = size,
            });
        }

        // ── GET /api/subjectofferings/by-doctor/{doctorId} ────────────────────
        /// <summary>
        /// Returns all offerings assigned to a doctor (by Doctor profile ID).
        /// Optional: ?semesterId=&page=1&size=20
        /// Doctor role: only their own offerings. Admin: any doctor.
        /// </summary>
        [HttpGet("by-doctor/{doctorId}")]
        [Authorize(Roles = "Admin,Doctor")]
        public async Task<IActionResult> GetByDoctor(
            string doctorId,
            [FromQuery] string? semesterId = null,
            [FromQuery] int     page       = 1,
            [FromQuery] int     size       = 20)
        {
            if (!Ulid.TryParse(doctorId, out var drId))
                return BadRequest("Invalid Doctor ID.");

            page = Math.Max(1, page);
            size = Math.Clamp(size, 1, 100);

            // Doctors can only query their own offerings
            var roleClaim = User.FindFirst(ClaimTypes.Role)?.Value ?? User.FindFirst("role")?.Value ?? "";
            if (roleClaim.Equals("Doctor", StringComparison.OrdinalIgnoreCase))
            {
                var userIdClaim = User.FindFirst("nameid")?.Value;
                if (userIdClaim == null) return Unauthorized();
                var callerUserId = Ulid.Parse(userIdClaim);
                var isOwn = await _context.Doctors
                    .AsNoTracking()
                    .AnyAsync(d => d.Id == drId && d.SystemUserId == callerUserId);
                if (!isOwn) return Forbid();
            }

            var q = _context.SubjectOfferings
                .AsNoTracking()
                .Where(o => o.DoctorId == drId);

            if (!string.IsNullOrWhiteSpace(semesterId) && Ulid.TryParse(semesterId, out var sId))
                q = q.Where(o => o.SemesterId == sId);

            var total = await q.CountAsync();

            var data = await q
                .OrderBy(o => o.Subject.Name)
                .Skip((page - 1) * size)
                .Take(size)
                .Select(o => new OfferingSummaryDto
                {
                    Id             = o.Id.ToString(),
                    Code           = o.Code,
                    SubjectName    = o.Subject != null ? o.Subject.Name : "",
                    SubjectCode    = o.Subject != null ? o.Subject.Code : "",
                    DoctorName     = o.Doctor != null ? o.Doctor.FullName : "",
                    DoctorId       = o.DoctorId.ToString(),
                    DepartmentName = o.Department != null ? o.Department.Name : "",
                    BatchName      = o.Batch != null ? o.Batch.Name : "",
                    SemesterName   = o.Semester != null ? o.Semester.Name : "",
                    MaxCapacity    = o.MaxCapacity,
                    EnrolledCount  = o.Enrollments.Count(e => e.IsActive && e.DeletedAt == null),
                })
                .ToListAsync();

            return Ok(new PagedResult<OfferingSummaryDto>
            {
                Data = data, TotalCount = total, Page = page, Size = size,
            });
        }

        // ── GET /api/subjectofferings/by-batch/{batchId} ──────────────────────
        /// <summary>
        /// Returns all offerings for a batch.
        /// Optional: ?departmentId=&semesterId=&page=1&size=20
        /// </summary>
        [HttpGet("by-batch/{batchId}")]
        [Authorize(Roles = "Admin,Doctor")]
        public async Task<IActionResult> GetByBatch(
            string batchId,
            [FromQuery] string? departmentId = null,
            [FromQuery] string? semesterId   = null,
            [FromQuery] int     page         = 1,
            [FromQuery] int     size         = 20)
        {
            if (!Ulid.TryParse(batchId, out var bId))
                return BadRequest("Invalid Batch ID.");

            page = Math.Max(1, page);
            size = Math.Clamp(size, 1, 100);

            var q = _context.SubjectOfferings
                .AsNoTracking()
                .Where(o => o.BatchId == bId);

            if (!string.IsNullOrWhiteSpace(departmentId) && Ulid.TryParse(departmentId, out var dId))
                q = q.Where(o => o.DepartmentId == dId);

            if (!string.IsNullOrWhiteSpace(semesterId) && Ulid.TryParse(semesterId, out var sId))
                q = q.Where(o => o.SemesterId == sId);

            var total = await q.CountAsync();

            var data = await q
                .OrderBy(o => o.Department.Name)
                .ThenBy(o => o.Subject.Name)
                .Skip((page - 1) * size)
                .Take(size)
                .Select(o => new OfferingSummaryDto
                {
                    Id             = o.Id.ToString(),
                    Code           = o.Code,
                    SubjectName    = o.Subject != null ? o.Subject.Name : "",
                    SubjectCode    = o.Subject != null ? o.Subject.Code : "",
                    DoctorName     = o.Doctor != null ? o.Doctor.FullName : "",
                    DoctorId       = o.DoctorId.ToString(),
                    DepartmentName = o.Department != null ? o.Department.Name : "",
                    BatchName      = o.Batch != null ? o.Batch.Name : "",
                    SemesterName   = o.Semester != null ? o.Semester.Name : "",
                    MaxCapacity    = o.MaxCapacity,
                    EnrolledCount  = o.Enrollments.Count(e => e.IsActive && e.DeletedAt == null),
                })
                .ToListAsync();

            return Ok(new PagedResult<OfferingSummaryDto>
            {
                Data = data, TotalCount = total, Page = page, Size = size,
            });
        }

        [HttpGet("my-enrollments")]
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> GetMyEnrollments()
        {
            var profileIdClaim = User.FindFirst("ProfileId");
            if (profileIdClaim == null) return Unauthorized("Student profile not found.");
            if (!Ulid.TryParse(profileIdClaim.Value, out var studentId)) return Unauthorized("Invalid student ID.");
            
            var enrollments = await _enrollmentService.GetStudentEnrollmentsAsync(studentId);

            var dtos = enrollments.Select(e => new EnrollmentDto
            {
                Id = e.Id,
                StudentId = e.StudentId,
                StudentName = e.Student?.FullName ?? string.Empty,
                SubjectOfferingId = e.SubjectOfferingId,
                SubjectCode = e.SubjectOffering?.Subject?.Code ?? string.Empty,
                SubjectName = e.SubjectOffering?.Subject?.Name ?? string.Empty,
                DepartmentName = e.SubjectOffering?.Department?.Name ?? string.Empty,
                DoctorName = e.SubjectOffering?.Doctor?.FullName ?? string.Empty,
                SemesterName = e.SubjectOffering?.Semester?.Name ?? string.Empty,
                EnrolledAt = e.EnrolledAt,
                IsActive = e.IsActive
            });

            return Ok(dtos);
        }
    }
}
