using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NUlid;
using UniversityManagementSystem.Core.DTOs;
using UniversityManagementSystem.Core.Entities;
using UniversityManagementSystem.Infrastructure.Data;

namespace UniversityManagementSystem.Api.Controllers
{
    /// <summary>
    /// Phase 4: Advanced analytics dashboards — attendance trends, grade distributions,
    /// at-risk students, course performance, department comparisons, and role dashboards.
    /// All queries use AsNoTracking + projection — no full entity loading.
    /// Route prefix matches existing AnalyticsController: /api/analytics/*
    /// </summary>
    [ApiController]
    [Route("api/analytics")]
    public class DashboardController(AppDbContext context) : ControllerBase
    {
        private readonly AppDbContext _context = context;

        private IQueryable<Student> ActiveStudents =>
            _context.Students
                .AsNoTracking()
                .Where(s => s.IsActive);

        // ── GET /api/analytics/attendance/trends ──────────────────────────────
        /// <summary>
        /// Weekly attendance % for the last N weeks for a given offering (by SubjectId).
        /// </summary>
        [HttpGet("attendance/trends")]
        [Authorize(Roles = "Doctor,Admin,SuperAdmin")]
        public async Task<IActionResult> AttendanceTrends(
            [FromQuery] string offeringId,
            [FromQuery] int weeks = 8)
        {
            if (!Ulid.TryParse(offeringId, out var oId))
                return BadRequest("Invalid offeringId.");

            weeks = Math.Clamp(weeks, 1, 52);

            var offering = await _context.SubjectOfferings
                .AsNoTracking()
                .Where(o => o.Id == oId && o.DeletedAt == null)
                .Select(o => new { o.SubjectId })
                .FirstOrDefaultAsync();

            if (offering == null)
                return NotFound("Offering not found.");

            var cutoff = DateTime.UtcNow.AddDays(-weeks * 7);

            var sessions = await _context.AttendanceSessions
                .AsNoTracking()
                .Where(s => s.SubjectId == offering.SubjectId && s.SessionDate >= cutoff && s.DeletedAt == null)
                .Select(s => new { s.Id, s.SessionDate })
                .ToListAsync();

            if (!sessions.Any())
                return Ok(new List<AttendanceTrendDto>());

            var sessionIds = sessions.Select(s => s.Id).ToList();

            var attendances = await _context.StudentAttendances
                .AsNoTracking()
                .Where(a => sessionIds.Contains(a.AttendanceSessionId) && a.DeletedAt == null)
                .Select(a => new { a.AttendanceSessionId, a.IsPresent })
                .ToListAsync();

            var attBySession = attendances
                .GroupBy(a => a.AttendanceSessionId)
                .ToDictionary(g => g.Key, g => new { Total = g.Count(), Present = g.Count(x => x.IsPresent) });

            var result = sessions
                .Select(s => new
                {
                    s.Id,
                    s.SessionDate,
                    Week = $"{s.SessionDate:yyyy}-W{System.Globalization.ISOWeek.GetWeekOfYear(s.SessionDate):D2}",
                })
                .GroupBy(s => s.Week)
                .OrderBy(g => g.Key)
                .Select(g =>
                {
                    var totalRecs  = g.Sum(s => attBySession.TryGetValue(s.Id, out var a) ? a.Total : 0);
                    var presentRecs= g.Sum(s => attBySession.TryGetValue(s.Id, out var a) ? a.Present : 0);
                    var pct = totalRecs > 0 ? Math.Round((double)presentRecs / totalRecs * 100, 1) : 0;
                    return new AttendanceTrendDto(g.Key, pct, g.Count(), presentRecs);
                })
                .ToList();

            return Ok(result);
        }

        // ── GET /api/analytics/grades/distribution ────────────────────────────
        [HttpGet("grades/distribution")]
        [Authorize(Roles = "Doctor,Admin,SuperAdmin")]
        public async Task<IActionResult> GradeDistribution([FromQuery] string offeringId)
        {
            if (!Ulid.TryParse(offeringId, out var oId))
                return BadRequest("Invalid offeringId.");

            var grades = await _context.StudentGrades
                .AsNoTracking()
                .Where(g => g.SubjectOfferingId == oId && g.IsFinalized && g.DeletedAt == null)
                .Select(g => g.FinalScore)
                .ToListAsync();

            if (!grades.Any())
                return Ok(new GradeDistributionDto(0, 0, 0, 0, new List<GradeHistogramBucket>()));

            var total     = grades.Count;
            var excellent = grades.Count(s => s >= 85);
            var good      = grades.Count(s => s >= 70 && s < 85);
            var average   = grades.Count(s => s >= 50 && s < 70);
            var failing   = grades.Count(s => s < 50);

            var histogram = new List<GradeHistogramBucket>
            {
                new("0-49",   grades.Count(s => s < 50)),
                new("50-59",  grades.Count(s => s >= 50 && s < 60)),
                new("60-69",  grades.Count(s => s >= 60 && s < 70)),
                new("70-79",  grades.Count(s => s >= 70 && s < 80)),
                new("80-89",  grades.Count(s => s >= 80 && s < 90)),
                new("90-100", grades.Count(s => s >= 90)),
            };

            return Ok(new GradeDistributionDto(
                Math.Round((double)excellent / total * 100, 1),
                Math.Round((double)good      / total * 100, 1),
                Math.Round((double)average   / total * 100, 1),
                Math.Round((double)failing   / total * 100, 1),
                histogram));
        }

        // ── GET /api/analytics/at-risk-students ───────────────────────────────
        [HttpGet("at-risk-students")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<IActionResult> AtRiskStudents([FromQuery] string? departmentId = null)
        {
            var studentsQ = ActiveStudents.AsQueryable();

            if (!string.IsNullOrWhiteSpace(departmentId) && Ulid.TryParse(departmentId, out var dId))
                studentsQ = studentsQ.Where(s => s.DepartmentId == dId);

            var students = await studentsQ
                .Select(s => new
                {
                    s.Id,
                    s.Code,
                    s.FullName,
                    DepartmentName = s.Department != null ? s.Department.Name : "",
                })
                .ToListAsync();

            var studentIds = students.Select(s => s.Id).ToList();

            var grades = await _context.StudentGrades
                .AsNoTracking()
                .Where(g => studentIds.Contains(g.StudentId) && g.IsFinalized && g.DeletedAt == null)
                .Select(g => new { g.StudentId, g.FinalScore, g.GradePoints })
                .ToListAsync();

            var attendances = await _context.StudentAttendances
                .AsNoTracking()
                .Where(a => studentIds.Contains(a.StudentId) && a.DeletedAt == null)
                .Select(a => new { a.StudentId, a.IsPresent })
                .ToListAsync();

            var gradesByStudent     = grades.GroupBy(g => g.StudentId).ToDictionary(g => g.Key, g => g.ToList());
            var attendanceByStudent = attendances.GroupBy(a => a.StudentId).ToDictionary(a => a.Key, a => a.ToList());

            var result = students
                .Select(s =>
                {
                    var sg = gradesByStudent.TryGetValue(s.Id, out var gl) ? gl : new();
                    var sa = attendanceByStudent.TryGetValue(s.Id, out var al) ? al : new();

                    var gpa     = sg.Any() ? (double?)Math.Round(sg.Average(x => x.GradePoints), 2) : null;
                    var attRate = sa.Any() ? Math.Round((double)sa.Count(x => x.IsPresent) / sa.Count * 100, 1) : 100.0;
                    var failing = sg.Count(x => x.FinalScore < 50);

                    string risk = "Low";
                    if ((gpa.HasValue && gpa < 1.5) || attRate < 60 || failing >= 3)
                        risk = "High";
                    else if ((gpa.HasValue && gpa < 2.0) || attRate < 75 || failing >= 1)
                        risk = "Medium";

                    return new AtRiskStudentDto
                    {
                        StudentId       = s.Id.ToString(),
                        StudentCode     = s.Code,
                        FullName        = s.FullName,
                        DepartmentName  = s.DepartmentName,
                        Gpa             = gpa,
                        AttendanceRate  = attRate,
                        FailingSubjects = failing,
                        RiskLevel       = risk,
                    };
                })
                .Where(s => s.RiskLevel != "Low")
                .OrderByDescending(s => s.RiskLevel == "High")
                .ThenBy(s => s.Gpa)
                .ToList();

            return Ok(result);
        }

        // ── GET /api/analytics/course-performance ─────────────────────────────
        [HttpGet("course-performance")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<IActionResult> CoursePerformance([FromQuery] string semesterId)
        {
            if (!Ulid.TryParse(semesterId, out var sId))
                return BadRequest("Invalid semesterId.");

            var offerings = await _context.SubjectOfferings
                .AsNoTracking()
                .Where(o => o.SemesterId == sId && o.DeletedAt == null)
                .Select(o => new { o.Id, SubjectName = o.Subject.Name, o.SubjectId })
                .ToListAsync();

            var offeringIds = offerings.Select(o => o.Id).ToList();

            var enrollmentCounts = await _context.Enrollments
                .AsNoTracking()
                .Where(e => offeringIds.Contains(e.SubjectOfferingId) && e.IsActive && e.DeletedAt == null)
                .GroupBy(e => e.SubjectOfferingId)
                .Select(g => new { OfferingId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.OfferingId, x => x.Count);

            var gradeStats = await _context.StudentGrades
                .AsNoTracking()
                .Where(g => offeringIds.Contains(g.SubjectOfferingId) && g.IsFinalized && g.DeletedAt == null)
                .GroupBy(g => g.SubjectOfferingId)
                .Select(g => new
                {
                    OfferingId = g.Key,
                    Avg   = g.Average(x => x.FinalScore),
                    Pass  = g.Count(x => x.FinalScore >= 50),
                    Total = g.Count(),
                })
                .ToListAsync();

            var subjectIds     = offerings.Select(o => o.SubjectId).Distinct().ToList();
            var subjectIdMap   = offerings.ToDictionary(o => o.Id, o => o.SubjectId);

            var sessions = await _context.AttendanceSessions
                .AsNoTracking()
                .Where(s => subjectIds.Contains(s.SubjectId) && s.DeletedAt == null)
                .Select(s => new { s.Id, s.SubjectId })
                .ToListAsync();

            var sessionIds = sessions.Select(s => s.Id).ToList();
            var attRecs    = await _context.StudentAttendances
                .AsNoTracking()
                .Where(a => sessionIds.Contains(a.AttendanceSessionId) && a.DeletedAt == null)
                .Select(a => new { a.AttendanceSessionId, a.IsPresent })
                .ToListAsync();

            var attBySession = attRecs
                .GroupBy(a => a.AttendanceSessionId)
                .ToDictionary(g => g.Key, g => new { Total = g.Count(), Present = g.Count(x => x.IsPresent) });

            var attBySubject = sessions
                .GroupBy(s => s.SubjectId)
                .ToDictionary(
                    g => g.Key,
                    g =>
                    {
                        var tot = g.Sum(s => attBySession.TryGetValue(s.Id, out var a) ? a.Total : 0);
                        var pre = g.Sum(s => attBySession.TryGetValue(s.Id, out var a) ? a.Present : 0);
                        return tot > 0 ? Math.Round((double)pre / tot * 100, 1) : 0.0;
                    });

            var gradeDict = gradeStats.ToDictionary(x => x.OfferingId);

            var result = offerings.Select(o =>
            {
                var gs      = gradeDict.TryGetValue(o.Id, out var g) ? g : null;
                var subjId  = subjectIdMap.TryGetValue(o.Id, out var sid) ? sid : default;
                var attRate = attBySubject.TryGetValue(subjId, out var ar) ? ar : 0.0;
                return new CoursePerformanceDto(
                    o.Id.ToString(),
                    o.SubjectName,
                    gs != null ? Math.Round(gs.Avg, 1) : 0,
                    attRate,
                    enrollmentCounts.TryGetValue(o.Id, out var ec) ? ec : 0,
                    gs != null && gs.Total > 0 ? Math.Round((double)gs.Pass / gs.Total * 100, 1) : 0);
            }).ToList();

            return Ok(result);
        }

        // ── GET /api/analytics/student/{studentId}/performance ────────────────
        [HttpGet("student/{studentId}/performance")]
        [Authorize(Roles = "Student,Doctor,Admin,SuperAdmin")]
        public async Task<IActionResult> StudentPerformance(string studentId)
        {
            if (!Ulid.TryParse(studentId, out var sId))
                return BadRequest("Invalid studentId.");

            // Students can only view their own performance
            var userRole = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role)?.Value
                          ?? User.Claims.FirstOrDefault(c => c.Type == "role")?.Value ?? "";

            if (userRole.Equals("Student", StringComparison.OrdinalIgnoreCase))
            {
                var profileId = User.Claims.FirstOrDefault(c => c.Type == "ProfileId")?.Value;
                if (profileId == null || profileId != sId.ToString())
                    return StatusCode(403);
            }

            var grades = await _context.StudentGrades
                .AsNoTracking()
                .Where(g => g.StudentId == sId && g.IsFinalized && g.DeletedAt == null)
                .Select(g => new
                {
                    SubjectName = g.SubjectOffering.Subject.Name,
                    g.FinalScore,
                    SubjectId = g.SubjectOffering.SubjectId,
                })
                .ToListAsync();

            var subjectIds = grades.Select(g => g.SubjectId).Distinct().ToList();

            var sessions = await _context.AttendanceSessions
                .AsNoTracking()
                .Where(s => subjectIds.Contains(s.SubjectId) && s.DeletedAt == null)
                .Select(s => new { s.Id, s.SubjectId })
                .ToListAsync();

            var sessionIds = sessions.Select(s => s.Id).ToList();
            var attRecs    = await _context.StudentAttendances
                .AsNoTracking()
                .Where(a => a.StudentId == sId && sessionIds.Contains(a.AttendanceSessionId) && a.DeletedAt == null)
                .Select(a => new { a.AttendanceSessionId, a.IsPresent })
                .ToListAsync();

            var sessionSubjectMap = sessions.ToDictionary(s => s.Id, s => s.SubjectId);
            var attBySubject = attRecs
                .GroupBy(a => sessionSubjectMap.TryGetValue(a.AttendanceSessionId, out var sid) ? sid : default)
                .Where(g => g.Key != default)
                .ToDictionary(g => g.Key, g => Math.Round((double)g.Count(x => x.IsPresent) / g.Count() * 100, 1));

            var result = grades.Select(g =>
            {
                var att    = attBySubject.TryGetValue(g.SubjectId, out var a) ? a : 100.0;
                var status = g.FinalScore >= 85 ? "Excellent"
                           : g.FinalScore >= 70 ? "Good"
                           : g.FinalScore >= 50 ? "Average"
                           : "Failing";
                return new StudentPerformanceDto(g.SubjectName, g.FinalScore, att, status);
            }).ToList();

            return Ok(result);
        }

        // ── GET /api/analytics/department/comparison ──────────────────────────
        [HttpGet("department/comparison")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<IActionResult> DepartmentComparison([FromQuery] string? academicYearId = null)
        {
            var departments = await _context.Departments
                .AsNoTracking()
                .Where(d => d.DeletedAt == null)
                .Select(d => new { d.Id, d.Name })
                .ToListAsync();

            var deptIds = departments.Select(d => d.Id).ToList();

            var studentDeptData = await ActiveStudents
                .Where(s => deptIds.Contains(s.DepartmentId))
                .Select(s => new { s.Id, s.DepartmentId })
                .ToListAsync();

            var studentDeptMap = studentDeptData.ToDictionary(s => s.Id, s => s.DepartmentId);
            var allStudentIds  = studentDeptMap.Keys.ToList();

            var grades = await _context.StudentGrades
                .AsNoTracking()
                .Where(g => allStudentIds.Contains(g.StudentId) && g.IsFinalized && g.DeletedAt == null)
                .Select(g => new { g.StudentId, g.GradePoints, g.FinalScore })
                .ToListAsync();

            var attendances = await _context.StudentAttendances
                .AsNoTracking()
                .Where(a => allStudentIds.Contains(a.StudentId) && a.DeletedAt == null)
                .Select(a => new { a.StudentId, a.IsPresent })
                .ToListAsync();

            var gradeByDept = grades
                .GroupBy(g => studentDeptMap.TryGetValue(g.StudentId, out var d) ? d : default)
                .Where(g => g.Key != default)
                .ToDictionary(
                    g => g.Key,
                    g => new
                    {
                        AvgGpa   = Math.Round(g.Average(x => x.GradePoints), 2),
                        PassRate = g.Any() ? Math.Round((double)g.Count(x => x.FinalScore >= 50) / g.Count() * 100, 1) : 0.0,
                    });

            var attByDept = attendances
                .GroupBy(a => studentDeptMap.TryGetValue(a.StudentId, out var d) ? d : default)
                .Where(g => g.Key != default)
                .ToDictionary(
                    g => g.Key,
                    g => g.Any() ? Math.Round((double)g.Count(x => x.IsPresent) / g.Count() * 100, 1) : 0.0);

            var countByDept = studentDeptData.GroupBy(s => s.DepartmentId).ToDictionary(g => g.Key, g => g.Count());

            var result = departments
                .Select(d =>
                {
                    var gd  = gradeByDept.TryGetValue(d.Id, out var g) ? g : null;
                    var att = attByDept.TryGetValue(d.Id, out var a) ? a : 0.0;
                    return new DepartmentComparisonDto(
                        d.Name,
                        gd?.AvgGpa ?? 0,
                        gd?.PassRate ?? 0,
                        att,
                        countByDept.TryGetValue(d.Id, out var c) ? c : 0);
                })
                .OrderByDescending(d => d.AvgGpa)
                .ToList();

            return Ok(result);
        }

        // ── GET /api/analytics/dashboard/admin ────────────────────────────────
        [HttpGet("dashboard/admin")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<IActionResult> AdminDashboard()
        {
            var totalStudents = await ActiveStudents.CountAsync();
            var totalDoctors  = await _context.Doctors.AsNoTracking().CountAsync(d => d.DeletedAt == null);
            var activeCourses = await _context.SubjectOfferings.AsNoTracking().CountAsync(o => o.DeletedAt == null);

            var allGrades = await _context.StudentGrades
                .AsNoTracking()
                .Where(g => g.IsFinalized && g.DeletedAt == null)
                .Select(g => new { g.GradePoints, g.FinalScore })
                .ToListAsync();

            var avgGpa   = allGrades.Any() ? Math.Round(allGrades.Average(g => g.GradePoints), 2) : 0.0;
            var passRate = allGrades.Any()
                ? Math.Round((double)allGrades.Count(g => g.FinalScore >= 50) / allGrades.Count * 100, 1)
                : 0.0;

            var atRiskCount = await _context.StudentGrades
                .AsNoTracking()
                .Where(g => g.IsFinalized && g.DeletedAt == null)
                .GroupBy(g => g.StudentId)
                .Select(g => new { Avg = g.Average(x => x.GradePoints) })
                .CountAsync(g => g.Avg < 2.0);

            return Ok(new AdminDashboardDto(totalStudents, totalDoctors, activeCourses, avgGpa, passRate, atRiskCount));
        }

        // ── GET /api/analytics/dashboard/doctor ───────────────────────────────
        [HttpGet("dashboard/doctor")]
        [Authorize(Roles = "Doctor,Admin,SuperAdmin")]
        public async Task<IActionResult> DoctorDashboard([FromQuery] string? doctorId = null)
        {
            Ulid drId;
            if (string.IsNullOrWhiteSpace(doctorId))
            {
                var profileId = User.Claims.FirstOrDefault(c => c.Type == "ProfileId")?.Value;
                if (profileId == null || !Ulid.TryParse(profileId, out drId))
                    return BadRequest("doctorId required or must be authenticated as Doctor with ProfileId claim.");
            }
            else
            {
                if (!Ulid.TryParse(doctorId, out drId))
                    return BadRequest("Invalid doctorId.");
            }

            var offerings = await _context.SubjectOfferings
                .AsNoTracking()
                .Where(o => o.DoctorId == drId && o.DeletedAt == null)
                .Select(o => new { o.Id, SubjectName = o.Subject.Name })
                .ToListAsync();

            var offeringIds = offerings.Select(o => o.Id).ToList();

            var enrollmentCounts = await _context.Enrollments
                .AsNoTracking()
                .Where(e => offeringIds.Contains(e.SubjectOfferingId) && e.IsActive && e.DeletedAt == null)
                .GroupBy(e => e.SubjectOfferingId)
                .Select(g => new { OfferingId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.OfferingId, x => x.Count);

            var gradeStats = await _context.StudentGrades
                .AsNoTracking()
                .Where(g => offeringIds.Contains(g.SubjectOfferingId) && g.IsFinalized && g.DeletedAt == null)
                .GroupBy(g => g.SubjectOfferingId)
                .Select(g => new
                {
                    OfferingId = g.Key,
                    Avg   = g.Average(x => x.FinalScore),
                    Pass  = g.Count(x => x.FinalScore >= 50),
                    Total = g.Count(),
                })
                .ToListAsync();

            var gradeDict    = gradeStats.ToDictionary(x => x.OfferingId);
            var totalStudents = enrollmentCounts.Values.Sum();
            var avgGrade      = gradeStats.Any() ? Math.Round(gradeStats.Average(g => g.Avg), 1) : 0.0;

            var courseList = offerings.Select(o =>
            {
                var gs = gradeDict.TryGetValue(o.Id, out var g) ? g : null;
                return new CoursePerformanceDto(
                    o.Id.ToString(),
                    o.SubjectName,
                    gs != null ? Math.Round(gs.Avg, 1) : 0,
                    0, // attendance per offering requires session linkage; placeholder
                    enrollmentCounts.TryGetValue(o.Id, out var ec) ? ec : 0,
                    gs != null && gs.Total > 0 ? Math.Round((double)gs.Pass / gs.Total * 100, 1) : 0);
            }).ToList();

            return Ok(new DoctorDashboardDto(offerings.Count, totalStudents, 0, avgGrade, courseList));
        }

        // ── GET /api/analytics/dashboard/student ──────────────────────────────
        [HttpGet("dashboard/student")]
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> StudentDashboard()
        {
            var profileId = User.Claims.FirstOrDefault(c => c.Type == "ProfileId")?.Value;
            if (profileId == null || !Ulid.TryParse(profileId, out var studentId))
                return Unauthorized("ProfileId claim missing.");

            var enrolledCourses = await _context.Enrollments
                .AsNoTracking()
                .CountAsync(e => e.StudentId == studentId && e.IsActive && e.DeletedAt == null);

            var grades = await _context.StudentGrades
                .AsNoTracking()
                .Where(g => g.StudentId == studentId && g.IsFinalized && g.DeletedAt == null)
                .Select(g => new
                {
                    SubjectName = g.SubjectOffering.Subject.Name,
                    g.FinalScore,
                    g.GradePoints,
                    SubjectId = g.SubjectOffering.SubjectId,
                })
                .ToListAsync();

            var currentGpa = grades.Any() ? Math.Round(grades.Average(g => g.GradePoints), 2) : 0.0;

            var subjectIds = grades.Select(g => g.SubjectId).Distinct().ToList();
            var sessions   = await _context.AttendanceSessions
                .AsNoTracking()
                .Where(s => subjectIds.Contains(s.SubjectId) && s.DeletedAt == null)
                .Select(s => new { s.Id, s.SubjectId })
                .ToListAsync();

            var sessionIds = sessions.Select(s => s.Id).ToList();
            var attRecs    = await _context.StudentAttendances
                .AsNoTracking()
                .Where(a => a.StudentId == studentId && sessionIds.Contains(a.AttendanceSessionId) && a.DeletedAt == null)
                .Select(a => new { a.AttendanceSessionId, a.IsPresent })
                .ToListAsync();

            var overallAtt = attRecs.Any()
                ? Math.Round((double)attRecs.Count(x => x.IsPresent) / attRecs.Count * 100, 1)
                : 100.0;

            var sessionSubjectMap = sessions.ToDictionary(s => s.Id, s => s.SubjectId);
            var attBySubject = attRecs
                .GroupBy(a => sessionSubjectMap.TryGetValue(a.AttendanceSessionId, out var sid) ? sid : default)
                .Where(g => g.Key != default)
                .ToDictionary(g => g.Key, g => Math.Round((double)g.Count(x => x.IsPresent) / g.Count() * 100, 1));

            var subjectDetails = grades.Select(g =>
            {
                var att    = attBySubject.TryGetValue(g.SubjectId, out var a) ? a : 100.0;
                var status = g.FinalScore >= 85 ? "Excellent"
                           : g.FinalScore >= 70 ? "Good"
                           : g.FinalScore >= 50 ? "Average"
                           : "Failing";
                return new StudentPerformanceDto(g.SubjectName, g.FinalScore, att, status);
            }).ToList();

            return Ok(new StudentDashboardDto(currentGpa, overallAtt, enrolledCourses, subjectDetails));
        }
    }
}
