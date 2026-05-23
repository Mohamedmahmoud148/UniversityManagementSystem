using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Hangfire;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NUlid;
using UniversityManagementSystem.Core.DTOs;
using UniversityManagementSystem.Core.Entities;
using UniversityManagementSystem.Core.Interfaces;
using UniversityManagementSystem.Infrastructure.Data;

namespace UniversityManagementSystem.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class RiskController(
        IAcademicRiskJob riskJob,
        AppDbContext context,
        IBackgroundJobClient backgroundJobClient) : ControllerBase
    {
        private readonly IAcademicRiskJob _riskJob = riskJob;
        private readonly AppDbContext _context = context;
        private readonly IBackgroundJobClient _backgroundJobClient = backgroundJobClient;

        /// <summary>
        /// Returns at-risk students for a specific SubjectOffering.
        /// Roles: Doctor, Admin, SuperAdmin
        /// </summary>
        [HttpGet("at-risk-students")]
        [Authorize(Roles = "Doctor,Admin,SuperAdmin")]
        public async Task<IActionResult> GetAtRiskStudents([FromQuery] string offeringId)
        {
            if (!Ulid.TryParse(offeringId, out var oid))
                return BadRequest("Invalid offering ID.");

            var students = await _riskJob.GetAtRiskStudentsAsync(oid);
            return Ok(students);
        }

        /// <summary>
        /// Returns all risk scores for a specific student (across all offerings).
        /// Roles: Student (own data only), Doctor, Admin, SuperAdmin
        /// </summary>
        [HttpGet("student/{studentId}")]
        [Authorize(Roles = "Student,Doctor,Admin,SuperAdmin")]
        public async Task<IActionResult> GetStudentRisk(string studentId)
        {
            if (!Ulid.TryParse(studentId, out var sid))
                return BadRequest("Invalid student ID.");

            var scores = await _context.AcademicRiskScores
                .AsNoTracking()
                .Include(s => s.SubjectOffering)
                    .ThenInclude(so => so.Subject)
                .Where(s => s.StudentId == sid && s.RiskLevel >= RiskLevel.Medium)
                .OrderByDescending(s => s.RiskLevel)
                .Select(s => new StudentRiskDto(
                    s.StudentId.ToString(),
                    s.Student.FullName,
                    s.SubjectOffering.Subject.Name,
                    s.AttendancePercent,
                    s.AverageGrade,
                    s.RiskLevel.ToString(),
                    s.AiRecommendation))
                .ToListAsync();

            return Ok(scores);
        }

        /// <summary>
        /// Manually triggers the full daily risk analysis job.
        /// Roles: Admin, SuperAdmin
        /// </summary>
        [HttpPost("analyze/trigger")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public IActionResult TriggerAnalysis()
        {
            _backgroundJobClient.Enqueue<IAcademicRiskJob>(job => job.RunDailyRiskAnalysisAsync());
            return Accepted(new { message = "Risk analysis job enqueued successfully." });
        }

        /// <summary>
        /// Returns all at-risk students across all active offerings (dashboard view).
        /// Roles: Admin, SuperAdmin
        /// </summary>
        [HttpGet("dashboard")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<IActionResult> GetDashboard()
        {
            var today = DateTime.UtcNow.Date;

            var scores = await _context.AcademicRiskScores
                .AsNoTracking()
                .Include(s => s.Student)
                .Include(s => s.SubjectOffering)
                    .ThenInclude(so => so.Subject)
                .Include(s => s.SubjectOffering)
                    .ThenInclude(so => so.Semester)
                .Where(s => s.RiskLevel >= RiskLevel.Medium
                         && s.SubjectOffering.Semester.EndDate >= today)
                .OrderByDescending(s => s.RiskLevel)
                    .ThenByDescending(s => s.AnalyzedAt)
                .Select(s => new StudentRiskDto(
                    s.StudentId.ToString(),
                    s.Student.FullName,
                    s.SubjectOffering.Subject.Name,
                    s.AttendancePercent,
                    s.AverageGrade,
                    s.RiskLevel.ToString(),
                    s.AiRecommendation))
                .ToListAsync();

            return Ok(new
            {
                TotalAtRisk = scores.Count,
                Critical = scores.Count(s => s.RiskLevel == "Critical"),
                High = scores.Count(s => s.RiskLevel == "High"),
                Medium = scores.Count(s => s.RiskLevel == "Medium"),
                Students = scores
            });
        }
    }
}
