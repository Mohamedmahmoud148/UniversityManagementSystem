using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NUlid;
using UniversityManagementSystem.Core.DTOs;
using UniversityManagementSystem.Infrastructure.Data;

namespace UniversityManagementSystem.Api.Controllers
{
    /// <summary>
    /// Lightweight analytics endpoints designed for AI orchestration and admin dashboards.
    /// All queries use AsNoTracking + Select projection — no full entity loading.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public class AnalyticsController(AppDbContext context) : ControllerBase
    {
        private readonly AppDbContext _context = context;

        // ── GET /api/analytics/student-count-by-department ────────────────────
        /// <summary>
        /// Returns student count per department, sorted descending.
        /// Includes doctor count per department in the same query.
        /// </summary>
        [HttpGet("student-count-by-department")]
        public async Task<IActionResult> StudentCountByDepartment()
        {
            var studentCounts = await _context.Students
                .AsNoTracking()
                .Where(s => s.DeletedAt == null)
                .GroupBy(s => new { s.DepartmentId })
                .Select(g => new { g.Key.DepartmentId, StudentCount = g.Count() })
                .ToListAsync();

            var doctorCounts = await _context.Doctors
                .AsNoTracking()
                .Where(d => d.DeletedAt == null)
                .GroupBy(d => d.DepartmentId)
                .Select(g => new { DepartmentId = g.Key, DoctorCount = g.Count() })
                .ToListAsync();

            var departments = await _context.Departments
                .AsNoTracking()
                .Include(d => d.College)
                .Select(d => new { d.Id, d.Name, CollegeName = d.College != null ? d.College.Name : "" })
                .ToListAsync();

            var studentDict = studentCounts.ToDictionary(x => x.DepartmentId, x => x.StudentCount);
            var doctorDict  = doctorCounts.ToDictionary(x => x.DepartmentId, x => x.DoctorCount);

            var result = departments
                .Select(d => new DepartmentCountDto
                {
                    DepartmentId   = d.Id.ToString(),
                    DepartmentName = d.Name,
                    CollegeName    = d.CollegeName,
                    StudentCount   = studentDict.TryGetValue(d.Id, out var sc) ? sc : 0,
                    DoctorCount    = doctorDict.TryGetValue(d.Id, out var dc) ? dc : 0,
                })
                .OrderByDescending(x => x.StudentCount)
                .ToList();

            return Ok(result);
        }

        // ── GET /api/analytics/student-count-by-batch ─────────────────────────
        /// <summary>
        /// Returns student count per batch, sorted descending.
        /// </summary>
        [HttpGet("student-count-by-batch")]
        public async Task<IActionResult> StudentCountByBatch()
        {
            var data = await _context.Students
                .AsNoTracking()
                .Where(s => s.DeletedAt == null)
                .GroupBy(s => new { s.BatchId })
                .Select(g => new { g.Key.BatchId, Count = g.Count() })
                .ToListAsync();

            var batches = await _context.Batches
                .AsNoTracking()
                .Include(b => b.Department)
                    .ThenInclude(d => d.College)
                .Select(b => new
                {
                    b.Id, b.Name, b.Code,
                    DepartmentName = b.Department != null ? b.Department.Name : "",
                    CollegeName    = b.Department != null && b.Department.College != null ? b.Department.College.Name : ""
                })
                .ToListAsync();

            var countDict = data.ToDictionary(x => x.BatchId, x => x.Count);

            var result = batches
                .Select(b => new BatchCountDto
                {
                    BatchId        = b.Id.ToString(),
                    BatchName      = b.Name,
                    BatchCode      = b.Code,
                    DepartmentName = b.DepartmentName,
                    CollegeName    = b.CollegeName,
                    StudentCount   = countDict.TryGetValue(b.Id, out var c) ? c : 0,
                })
                .OrderByDescending(x => x.StudentCount)
                .ToList();

            return Ok(result);
        }

        // ── GET /api/analytics/doctor-workload ────────────────────────────────
        /// <summary>
        /// Returns each doctor's offering count and total enrolled students.
        /// Sorted by total students descending.
        /// Optional: ?departmentId=ULID&collegeId=ULID
        /// </summary>
        [HttpGet("doctor-workload")]
        public async Task<IActionResult> DoctorWorkload(
            [FromQuery] string? departmentId = null,
            [FromQuery] string? collegeId    = null)
        {
            var offeringsQ = _context.SubjectOfferings.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(departmentId) && Ulid.TryParse(departmentId, out var deptId))
                offeringsQ = offeringsQ.Where(o => o.DepartmentId == deptId);

            if (!string.IsNullOrWhiteSpace(collegeId) && Ulid.TryParse(collegeId, out var colId))
                offeringsQ = offeringsQ.Where(o => o.Department.CollegeId == colId);

            // Aggregate offering count + enrolled count per doctor in one pass
            var workload = await offeringsQ
                .GroupBy(o => o.DoctorId)
                .Select(g => new
                {
                    DoctorId      = g.Key,
                    OfferingCount = g.Count(),
                    TotalStudents = g.SelectMany(o => o.Enrollments).Count(e => e.IsActive)
                })
                .ToListAsync();

            var doctorIds = workload.Select(w => w.DoctorId).ToList();

            var doctors = await _context.Doctors
                .AsNoTracking()
                .Include(d => d.Department)
                .Where(d => doctorIds.Contains(d.Id) && d.DeletedAt == null)
                .Select(d => new
                {
                    d.Id, d.Code, d.FullName,
                    DepartmentName = d.Department != null ? d.Department.Name : ""
                })
                .ToListAsync();

            var doctorDict = doctors.ToDictionary(d => d.Id);

            var result = workload
                .Where(w => doctorDict.ContainsKey(w.DoctorId))
                .Select(w => new DoctorWorkloadDto
                {
                    DoctorId       = w.DoctorId.ToString(),
                    DoctorCode     = doctorDict[w.DoctorId].Code,
                    FullName       = doctorDict[w.DoctorId].FullName,
                    DepartmentName = doctorDict[w.DoctorId].DepartmentName,
                    OfferingCount  = w.OfferingCount,
                    TotalStudents  = w.TotalStudents,
                })
                .OrderByDescending(x => x.TotalStudents)
                .ToList();

            return Ok(result);
        }

        // ── GET /api/analytics/top-enrolled-subjects ──────────────────────────
        /// <summary>
        /// Returns subjects ranked by total enrollments across all their offerings.
        /// Optional: ?top=10
        /// </summary>
        [HttpGet("top-enrolled-subjects")]
        public async Task<IActionResult> TopEnrolledSubjects([FromQuery] int top = 10)
        {
            top = Math.Clamp(top, 1, 100);

            var result = await _context.Enrollments
                .AsNoTracking()
                .Where(e => e.IsActive && e.DeletedAt == null)
                .GroupBy(e => new
                {
                    e.SubjectOffering.SubjectId,
                    SubjectCode = e.SubjectOffering.Subject.Code,
                    SubjectName = e.SubjectOffering.Subject.Name,
                })
                .Select(g => new SubjectEnrollmentStatsDto
                {
                    SubjectId     = g.Key.SubjectId.ToString(),
                    SubjectCode   = g.Key.SubjectCode,
                    SubjectName   = g.Key.SubjectName,
                    OfferingCount = g.Select(e => e.SubjectOfferingId).Distinct().Count(),
                    EnrolledCount = g.Count(),
                })
                .OrderByDescending(x => x.EnrolledCount)
                .Take(top)
                .ToListAsync();

            return Ok(result);
        }

        // ── GET /api/analytics/offering-enrollment-stats ──────────────────────
        /// <summary>
        /// Enrollment stats per offering with fill-rate and average grade.
        /// Optional filters: ?departmentId=&batchId=&doctorId=&semesterId=
        /// Paginated: ?page=1&size=20
        /// </summary>
        [HttpGet("offering-enrollment-stats")]
        public async Task<IActionResult> OfferingEnrollmentStats(
            [FromQuery] string? departmentId = null,
            [FromQuery] string? batchId      = null,
            [FromQuery] string? doctorId     = null,
            [FromQuery] string? semesterId   = null,
            [FromQuery] int     page         = 1,
            [FromQuery] int     size         = 20)
        {
            page = Math.Max(1, page);
            size = Math.Clamp(size, 1, 100);

            var q = _context.SubjectOfferings
                .AsNoTracking()
                .Include(o => o.Subject)
                .Include(o => o.Doctor)
                .Include(o => o.Department)
                .Include(o => o.Semester)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(departmentId) && Ulid.TryParse(departmentId, out var dId))
                q = q.Where(o => o.DepartmentId == dId);

            if (!string.IsNullOrWhiteSpace(batchId) && Ulid.TryParse(batchId, out var bId))
                q = q.Where(o => o.BatchId == bId);

            if (!string.IsNullOrWhiteSpace(doctorId) && Ulid.TryParse(doctorId, out var drId))
                q = q.Where(o => o.DoctorId == drId);

            if (!string.IsNullOrWhiteSpace(semesterId) && Ulid.TryParse(semesterId, out var sId))
                q = q.Where(o => o.SemesterId == sId);

            var total = await q.CountAsync();

            var offerings = await q
                .OrderBy(o => o.Department.Name)
                .ThenBy(o => o.Subject.Name)
                .Skip((page - 1) * size)
                .Take(size)
                .ToListAsync();

            var offeringIds = offerings.Select(o => o.Id).ToList();

            // Batch-load enrollment counts
            var enrollmentCounts = await _context.Enrollments
                .AsNoTracking()
                .Where(e => offeringIds.Contains(e.SubjectOfferingId) && e.IsActive && e.DeletedAt == null)
                .GroupBy(e => e.SubjectOfferingId)
                .Select(g => new { OfferingId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.OfferingId, x => x.Count);

            // Batch-load average grades
            var avgGrades = await _context.StudentGrades
                .AsNoTracking()
                .Where(g => offeringIds.Contains(g.SubjectOfferingId) && g.IsFinalized)
                .GroupBy(g => g.SubjectOfferingId)
                .Select(g => new { OfferingId = g.Key, Avg = g.Average(x => x.FinalScore) })
                .ToDictionaryAsync(x => x.OfferingId, x => (double?)x.Avg);

            var data = offerings.Select(o =>
            {
                var enrolled = enrollmentCounts.TryGetValue(o.Id, out var ec) ? ec : 0;
                var fillRate = o.MaxCapacity > 0 ? Math.Round((double)enrolled / o.MaxCapacity * 100, 1) : 0;
                return new OfferingEnrollmentStatsDto
                {
                    OfferingId     = o.Id.ToString(),
                    OfferingCode   = o.Code,
                    SubjectName    = o.Subject?.Name ?? "",
                    DoctorName     = o.Doctor?.FullName ?? "",
                    DepartmentName = o.Department?.Name ?? "",
                    SemesterName   = o.Semester?.Name ?? "",
                    EnrolledCount  = enrolled,
                    MaxCapacity    = o.MaxCapacity,
                    FillRate       = fillRate,
                    AverageGrade   = avgGrades.TryGetValue(o.Id, out var ag) ? ag : null,
                };
            }).ToList();

            return Ok(new PagedResult<OfferingEnrollmentStatsDto>
            {
                Data       = data,
                TotalCount = total,
                Page       = page,
                Size       = size,
            });
        }

        // ── GET /api/analytics/summary ────────────────────────────────────────
        /// <summary>
        /// Quick system-wide summary — counts + top departments + top subjects.
        /// All counts in a single multi-query batch.
        /// </summary>
        [HttpGet("summary")]
        public async Task<IActionResult> Summary()
        {
            var studentCount    = await _context.Students.AsNoTracking().CountAsync(s => s.DeletedAt == null);
            var doctorCount     = await _context.Doctors.AsNoTracking().CountAsync(d => d.DeletedAt == null);
            var offeringCount   = await _context.SubjectOfferings.AsNoTracking().CountAsync();
            var enrollmentCount = await _context.Enrollments.AsNoTracking().CountAsync(e => e.IsActive && e.DeletedAt == null);
            var collegeCount    = await _context.Colleges.AsNoTracking().CountAsync(c => c.DeletedAt == null);
            var deptCount       = await _context.Departments.AsNoTracking().CountAsync(d => d.DeletedAt == null);
            var batchCount      = await _context.Batches.AsNoTracking().CountAsync(b => b.DeletedAt == null);

            var topDepts = await _context.Students
                .AsNoTracking()
                .Where(s => s.DeletedAt == null)
                .GroupBy(s => new { s.DepartmentId })
                .Select(g => new { g.Key.DepartmentId, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .Take(5)
                .Join(_context.Departments.Include(d => d.College),
                      sc => sc.DepartmentId,
                      d  => d.Id,
                      (sc, d) => new DepartmentCountDto
                      {
                          DepartmentId   = d.Id.ToString(),
                          DepartmentName = d.Name,
                          CollegeName    = d.College != null ? d.College.Name : "",
                          StudentCount   = sc.Count,
                      })
                .ToListAsync();

            var topSubjects = await _context.Enrollments
                .AsNoTracking()
                .Where(e => e.IsActive && e.DeletedAt == null)
                .GroupBy(e => new
                {
                    e.SubjectOffering.SubjectId,
                    SubjectCode = e.SubjectOffering.Subject.Code,
                    SubjectName = e.SubjectOffering.Subject.Name,
                })
                .Select(g => new SubjectEnrollmentStatsDto
                {
                    SubjectId     = g.Key.SubjectId.ToString(),
                    SubjectCode   = g.Key.SubjectCode,
                    SubjectName   = g.Key.SubjectName,
                    OfferingCount = g.Select(e => e.SubjectOfferingId).Distinct().Count(),
                    EnrolledCount = g.Count(),
                })
                .OrderByDescending(x => x.EnrolledCount)
                .Take(5)
                .ToListAsync();

            return Ok(new AnalyticsSummaryDto
            {
                TotalStudents    = studentCount,
                TotalDoctors     = doctorCount,
                TotalOfferings   = offeringCount,
                TotalEnrollments = enrollmentCount,
                TotalColleges    = collegeCount,
                TotalDepartments = deptCount,
                TotalBatches     = batchCount,
                TopDepartments   = topDepts,
                TopSubjects      = topSubjects,
            });
        }
    }
}
