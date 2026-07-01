using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NUlid;
using UniversityManagementSystem.Core.DTOs.TeachingIntelligence;
using UniversityManagementSystem.Core.Interfaces;

namespace UniversityManagementSystem.Api.Controllers;

/// <summary>
/// AI Teaching Intelligence Platform — Doctor-facing analytics API.
///
/// All endpoints require Doctor or SuperAdmin role.
/// Data is served from pre-computed StudentIntelligenceSnapshot rows
/// (refreshed hourly by TeachingIntelligenceBackgroundService).
///
/// Manual refresh endpoint available for real-time data.
/// </summary>
[Route("api/teaching-intelligence")]
[ApiController]
[Authorize(Roles = "Doctor,SuperAdmin")]
public class TeachingIntelligenceController(
    ITeachingIntelligenceService service,
    IUserContextService userContext,
    IServiceScopeFactory scopeFactory,
    ILogger<TeachingIntelligenceController> logger) : ControllerBase
{
    // ── Dashboard ──────────────────────────────────────────────────────────

    /// <summary>
    /// Full AI teaching dashboard.
    /// Returns all offerings summary + at-risk students + weak topics + AI recommendations.
    /// Cached — returns within 200ms.
    /// </summary>
    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard()
    {
        var userId = userContext.GetUserId();
        var dashboard = await service.GetDashboardAsync(userId);
        return Ok(dashboard);
    }

    /// <summary>
    /// List all subject offerings taught by this doctor with health indicators.
    /// </summary>
    [HttpGet("offerings")]
    public async Task<IActionResult> GetOfferings()
    {
        var userId = userContext.GetUserId();
        var offerings = await service.GetDoctorOfferingsAsync(userId);
        return Ok(offerings);
    }

    // ── Class analytics ───────────────────────────────────────────────────

    /// <summary>
    /// Full analytics for a specific subject offering.
    /// Includes grade distribution, attendance trends, topic performance, student list.
    /// </summary>
    [HttpGet("offerings/{offeringId}/analytics")]
    public async Task<IActionResult> GetClassAnalytics(
        string offeringId,
        [FromQuery] string? riskLevel = null,
        [FromQuery] bool atRiskOnly = false,
        [FromQuery] string? trend = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string sortBy = "RiskScore",
        [FromQuery] string sortDir = "desc")
    {
        if (!Ulid.TryParse(offeringId, out var id))
            return BadRequest("Invalid offering ID.");

        var filter = new TeachingQueryFilter(
            OfferingId: offeringId,
            RiskLevel: riskLevel,
            AtRiskOnly: atRiskOnly,
            Trend: trend,
            Page: page,
            PageSize: Math.Min(pageSize, 200),
            SortBy: sortBy,
            SortDir: sortDir
        );

        try
        {
            var userId = userContext.GetUserId();
            var result = await service.GetClassIntelligenceAsync(id, userId, filter);
            return Ok(result);
        }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    /// <summary>
    /// Compare performance across classes (groups/batches) for doctor's offerings.
    /// Useful for identifying which class needs the most attention.
    /// </summary>
    [HttpGet("compare")]
    public async Task<IActionResult> CompareClasses([FromQuery] string? subjectName = null)
    {
        var userId = userContext.GetUserId();
        var comparisons = await service.GetClassComparisonAsync(subjectName ?? "", userId);
        return Ok(comparisons);
    }

    // ── Student analytics ─────────────────────────────────────────────────

    /// <summary>
    /// Get paginated student analytics for a subject offering.
    /// Supports filtering by risk level, trend, and sorting.
    /// </summary>
    [HttpGet("offerings/{offeringId}/students")]
    public async Task<IActionResult> GetStudents(
        string offeringId,
        [FromQuery] string? riskLevel = null,
        [FromQuery] bool atRiskOnly = false,
        [FromQuery] string? trend = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string sortBy = "RiskScore",
        [FromQuery] string sortDir = "desc")
    {
        if (!Ulid.TryParse(offeringId, out var id))
            return BadRequest("Invalid offering ID.");

        var filter = new TeachingQueryFilter(
            OfferingId: offeringId,
            RiskLevel: riskLevel,
            AtRiskOnly: atRiskOnly,
            Trend: trend,
            Page: page,
            PageSize: Math.Min(pageSize, 200),
            SortBy: sortBy,
            SortDir: sortDir
        );

        try
        {
            var userId = userContext.GetUserId();
            var students = await service.GetStudentsAsync(id, userId, filter);
            return Ok(students);
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    /// <summary>
    /// Get detailed analytics for a single student in a specific offering.
    /// </summary>
    [HttpGet("offerings/{offeringId}/students/{studentId}")]
    public async Task<IActionResult> GetStudentAnalytics(
        string offeringId, string studentId)
    {
        if (!Ulid.TryParse(offeringId, out var oid) ||
            !Ulid.TryParse(studentId, out var sid))
            return BadRequest("Invalid ID format.");

        try
        {
            var userId = userContext.GetUserId();
            var result = await service.GetStudentAnalyticsAsync(sid, oid, userId);
            return result == null ? NotFound("Student analytics not found.") : Ok(result);
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    /// <summary>
    /// Get all at-risk students across ALL of this doctor's offerings.
    /// Default: medium + high + critical risk levels.
    /// </summary>
    [HttpGet("students/at-risk")]
    public async Task<IActionResult> GetAtRiskStudents(
        [FromQuery] string minRiskLevel = "medium")
    {
        var userId = userContext.GetUserId();
        var students = await service.GetAtRiskStudentsAsync(userId, minRiskLevel);
        return Ok(students);
    }

    /// <summary>
    /// Get the most improved students for a specific offering.
    /// Based on positive grade + attendance trend.
    /// </summary>
    [HttpGet("offerings/{offeringId}/students/most-improved")]
    public async Task<IActionResult> GetMostImproved(
        string offeringId, [FromQuery] int limit = 10)
    {
        if (!Ulid.TryParse(offeringId, out var id))
            return BadRequest("Invalid offering ID.");

        var userId = userContext.GetUserId();
        var students = await service.GetMostImprovedAsync(id, userId, limit);
        return Ok(students);
    }

    // ── Topic analytics ───────────────────────────────────────────────────

    /// <summary>
    /// Get topic-level analytics: weak/strong topics, question performance breakdown.
    /// Derived from exam and quiz submission data.
    /// </summary>
    [HttpGet("offerings/{offeringId}/topics")]
    public async Task<IActionResult> GetTopicAnalytics(string offeringId)
    {
        if (!Ulid.TryParse(offeringId, out var id))
            return BadRequest("Invalid offering ID.");

        try
        {
            var userId = userContext.GetUserId();
            var result = await service.GetTopicAnalyticsAsync(id, userId);
            return Ok(result);
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    // ── Alerts & Insights ────────────────────────────────────────────────

    /// <summary>
    /// Get AI-generated teaching alerts (attendance drops, risk escalations, etc.).
    /// </summary>
    [HttpGet("alerts")]
    public async Task<IActionResult> GetAlerts([FromQuery] bool unreadOnly = false)
    {
        var userId = userContext.GetUserId();
        var alerts = await service.GetAlertsAsync(userId, unreadOnly);
        return Ok(alerts);
    }

    /// <summary>Mark a teaching alert as read.</summary>
    [HttpPost("alerts/{alertId}/read")]
    public async Task<IActionResult> MarkAlertRead(string alertId)
    {
        if (!Ulid.TryParse(alertId, out var id))
            return BadRequest("Invalid alert ID.");

        try
        {
            var userId = userContext.GetUserId();
            await service.MarkAlertReadAsync(id, userId);
            return NoContent();
        }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
    }

    /// <summary>
    /// Get AI-generated insights and recommendations for the doctor.
    /// Optionally filtered by a specific offering.
    /// </summary>
    [HttpGet("insights")]
    public async Task<IActionResult> GetInsights([FromQuery] string? offeringId = null)
    {
        var userId = userContext.GetUserId();
        var insights = await service.GetAiInsightsAsync(userId, offeringId);
        return Ok(insights);
    }

    // ── Excel Export ──────────────────────────────────────────────────────

    /// <summary>
    /// Export student analytics for a subject offering as structured JSON.
    /// The frontend renders this as an Excel file using the column structure.
    ///
    /// Response includes:
    ///   - ExportMetadata (title, date, doctor name, subject/batch info)
    ///   - Rows: array of student records with 29 columns
    ///
    /// Frontend column order: UniversityId, Name, Batch, Group, Department,
    /// College, Subject, FinalScore, Midterm, Coursework, FinalExam, Grade,
    /// TotalSessions, Attended, Attendance%, TotalAssignments, Submitted,
    /// Missing, Completion%, TotalExams, AvgExamScore, AvgQuizScore,
    /// RiskScore, RiskLevel, RiskFactors, AiSessions, StudyMinutes, StreakDays
    /// </summary>
    [HttpGet("offerings/{offeringId}/export")]
    public async Task<IActionResult> ExportStudentData(string offeringId)
    {
        if (!Ulid.TryParse(offeringId, out var id))
            return BadRequest("Invalid offering ID.");

        try
        {
            var userId = userContext.GetUserId();
            var data = await service.GetStudentExportDataAsync(id, userId);
            return Ok(data);
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    /// <summary>
    /// Export all students in a batch (across all of doctor's offerings for that batch).
    /// Optionally filter by a specific offering.
    /// </summary>
    [HttpGet("batches/{batchId}/export")]
    public async Task<IActionResult> ExportBatchData(
        string batchId,
        [FromQuery] string? offeringId = null)
    {
        if (!Ulid.TryParse(batchId, out var bid))
            return BadRequest("Invalid batch ID.");

        Ulid? oid = null;
        if (!string.IsNullOrEmpty(offeringId) && Ulid.TryParse(offeringId, out var parsedOid))
            oid = parsedOid;

        try
        {
            var userId = userContext.GetUserId();
            var data = await service.GetBatchExportDataAsync(bid, userId, oid);
            return Ok(data);
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    /// <summary>
    /// Download a blank grades-entry template (.xlsx) for a subject offering.
    ///
    /// Columns: University ID | Full Name | Midterm | Coursework | Final Exam
    /// Column headers match the dynamic keyword detection in ImportGradesFromExcelAsync,
    /// so the doctor can fill in grades and re-upload this file without errors.
    /// </summary>
    [HttpGet("offerings/{offeringId}/grades-template")]
    public async Task<IActionResult> DownloadGradesTemplate(string offeringId)
    {
        if (!Ulid.TryParse(offeringId, out var id))
            return BadRequest("Invalid offering ID.");

        try
        {
            var userId = userContext.GetUserId();
            var (bytes, fileName) = await service.GenerateGradesTemplateAsync(id, userId);
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    // ── Manual snapshot refresh ───────────────────────────────────────────

    /// <summary>
    /// Manually trigger a snapshot refresh for a specific offering.
    /// Use this when you need real-time data (snapshots are normally refreshed hourly).
    /// Response: 202 Accepted (refresh runs asynchronously).
    /// </summary>
    [HttpPost("offerings/{offeringId}/refresh")]
    public IActionResult RefreshSnapshot(string offeringId)
    {
        if (!Ulid.TryParse(offeringId, out var id))
            return BadRequest("Invalid offering ID.");

        // Fire-and-forget on its own DI scope — the request-scoped `service`/its
        // AppDbContext gets disposed as soon as this action returns, so reusing it
        // here would throw ObjectDisposedException (silently swallowed by the
        // catch below), meaning the refresh never actually ran.
        _ = Task.Run(async () =>
        {
            using var scope = scopeFactory.CreateScope();
            var scopedService = scope.ServiceProvider.GetRequiredService<ITeachingIntelligenceService>();
            try { await scopedService.RefreshSnapshotAsync(id); }
            catch (Exception ex)
            {
                logger.LogError(ex, "RefreshSnapshot: failed for offering {OfferingId}", id);
            }
        });

        return Accepted(new { message = "Snapshot refresh started. Data will be updated shortly." });
    }

    /// <summary>
    /// Refresh ALL snapshots for all offerings taught by this doctor.
    /// Response: 202 Accepted.
    /// </summary>
    [HttpPost("refresh-all")]
    public IActionResult RefreshAll()
    {
        var userId = userContext.GetUserId();
        _ = Task.Run(async () =>
        {
            using var scope = scopeFactory.CreateScope();
            var scopedService = scope.ServiceProvider.GetRequiredService<ITeachingIntelligenceService>();
            try { await scopedService.RefreshAllDoctorSnapshotsAsync(userId); }
            catch (Exception ex)
            {
                logger.LogError(ex, "RefreshAll: failed for doctor user {UserId}", userId);
            }
        });
        return Accepted(new { message = "Full snapshot refresh started for all your offerings." });
    }
}
