using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UniversityManagementSystem.Core.Constants;
using UniversityManagementSystem.Core.Entities;
using UniversityManagementSystem.Infrastructure.Data;

namespace UniversityManagementSystem.Api.Controllers
{
    [ApiController]
    [Route("api/auditlogs")]
    [Authorize(Roles = "SuperAdmin,Admin")]
    public class AuditAnalyticsController(AppDbContext context) : ControllerBase
    {
        // ── GET /api/auditlogs/dashboard ─────────────────────────────────────
        [HttpGet("dashboard")]
        public async Task<IActionResult> Dashboard()
        {
            var today = DateTime.UtcNow.Date;
            var todayEnd = today.AddDays(1);

            var totalLogs       = await context.AuditLogs.CountAsync();
            var todayLogs       = await context.AuditLogs.CountAsync(l => l.Timestamp >= today && l.Timestamp < todayEnd);
            var criticalEvents  = await context.AuditLogs.CountAsync(l => l.Severity == AuditSeverity.Critical || l.Severity == AuditSeverity.Security);
            var failedActions   = await context.AuditLogs.CountAsync(l => l.Status == "Failed");
            var securityAlerts  = await context.AuditLogs.CountAsync(l => l.Severity == AuditSeverity.Security);
            var activeUsers     = await context.AuditLogs
                .Where(l => l.Timestamp >= today && l.UserId != null)
                .Select(l => l.UserId)
                .Distinct()
                .CountAsync();

            return Ok(new { totalLogs, todayLogs, criticalEvents, failedActions, activeUsers, securityAlerts });
        }

        // ── GET /api/auditlogs/timeline?hours=24 ─────────────────────────────
        [HttpGet("timeline")]
        public async Task<IActionResult> Timeline([FromQuery] int hours = 24)
        {
            if (hours < 1 || hours > 168) hours = 24;
            var from = DateTime.UtcNow.AddHours(-hours);

            var logs = await context.AuditLogs
                .Where(l => l.Timestamp >= from)
                .Select(l => new { l.Timestamp, l.Severity })
                .ToListAsync();

            var grouped = logs
                .GroupBy(l => new DateTime(l.Timestamp.Year, l.Timestamp.Month, l.Timestamp.Day, l.Timestamp.Hour, 0, 0, DateTimeKind.Utc))
                .Select(g => new
                {
                    hour          = g.Key,
                    count         = g.Count(),
                    criticalCount = g.Count(x => x.Severity == AuditSeverity.Critical || x.Severity == AuditSeverity.Security)
                })
                .OrderBy(x => x.hour)
                .ToList();

            return Ok(grouped);
        }

        // ── GET /api/auditlogs/top-users?limit=10 ────────────────────────────
        [HttpGet("top-users")]
        public async Task<IActionResult> TopUsers([FromQuery] int limit = 10)
        {
            if (limit < 1 || limit > 50) limit = 10;

            var result = await context.AuditLogs
                .Where(l => l.UserId != null && l.UserName != null)
                .GroupBy(l => new { l.UserId, l.UserName, l.Role })
                .Select(g => new
                {
                    userId      = g.Key.UserId.ToString(),
                    userName    = g.Key.UserName,
                    role        = g.Key.Role,
                    actionCount = g.Count(),
                    lastActive  = g.Max(x => x.Timestamp)
                })
                .OrderByDescending(x => x.actionCount)
                .Take(limit)
                .ToListAsync();

            return Ok(result);
        }

        // ── GET /api/auditlogs/top-modules?limit=10 ──────────────────────────
        [HttpGet("top-modules")]
        public async Task<IActionResult> TopModules([FromQuery] int limit = 10)
        {
            if (limit < 1 || limit > 50) limit = 10;

            var result = await context.AuditLogs
                .GroupBy(l => l.Entity)
                .Select(g => new
                {
                    entity      = g.Key,
                    actionCount = g.Count(),
                    lastAction  = g.Max(x => x.Timestamp)
                })
                .OrderByDescending(x => x.actionCount)
                .Take(limit)
                .ToListAsync();

            return Ok(result);
        }

        // ── GET /api/auditlogs/heatmap?days=30 ───────────────────────────────
        [HttpGet("heatmap")]
        public async Task<IActionResult> Heatmap([FromQuery] int days = 30)
        {
            if (days < 1 || days > 365) days = 30;
            var from = DateTime.UtcNow.AddDays(-days);

            var logs = await context.AuditLogs
                .Where(l => l.Timestamp >= from)
                .Select(l => new { l.Timestamp })
                .ToListAsync();

            var heatmap = logs
                .GroupBy(l => new { DayOfWeek = (int)l.Timestamp.DayOfWeek, Hour = l.Timestamp.Hour })
                .Select(g => new { g.Key.DayOfWeek, g.Key.Hour, count = g.Count() })
                .OrderBy(x => x.DayOfWeek).ThenBy(x => x.Hour)
                .ToList();

            return Ok(heatmap);
        }

        // ── GET /api/auditlogs/insights ───────────────────────────────────────
        [HttpGet("insights")]
        public async Task<IActionResult> Insights()
        {
            var now = DateTime.UtcNow;
            var today = now.Date;
            var yesterday = today.AddDays(-1);
            var thisWeek = today.AddDays(-7);
            var lastWeek = today.AddDays(-14);

            var insights = new System.Collections.Generic.List<object>();

            // Failed logins today
            var failedToday = await context.AuditLogs
                .CountAsync(l => l.Action == AuditActions.FailedLogin && l.Timestamp >= today);
            if (failedToday > 0)
                insights.Add(new { type = "warning", message = $"{failedToday} failed login attempt(s) detected today" });

            // Critical events in last 24h
            var criticalCount = await context.AuditLogs
                .CountAsync(l => (l.Severity == AuditSeverity.Critical || l.Severity == AuditSeverity.Security) && l.Timestamp >= now.AddHours(-24));
            if (criticalCount > 0)
                insights.Add(new { type = "critical", message = $"{criticalCount} critical/security event(s) in the last 24 hours" });

            // Top user today
            var topUser = await context.AuditLogs
                .Where(l => l.Timestamp >= today && l.UserName != null)
                .GroupBy(l => l.UserName)
                .Select(g => new { name = g.Key, count = g.Count() })
                .OrderByDescending(x => x.count)
                .FirstOrDefaultAsync();
            if (topUser != null)
                insights.Add(new { type = "info", message = $"{topUser.name} performed {topUser.count} action(s) today" });

            // Grade imports this week vs last week
            var importsThisWeek = await context.AuditLogs
                .CountAsync(l => l.Action == AuditActions.ImportGrades && l.Timestamp >= thisWeek);
            var importsLastWeek = await context.AuditLogs
                .CountAsync(l => l.Action == AuditActions.ImportGrades && l.Timestamp >= lastWeek && l.Timestamp < thisWeek);
            if (importsLastWeek > 0)
            {
                var pct = (int)Math.Round((importsThisWeek - importsLastWeek) * 100.0 / importsLastWeek);
                if (pct != 0)
                    insights.Add(new { type = pct > 0 ? "info" : "warning", message = $"Grade imports {(pct > 0 ? "increased" : "decreased")} by {Math.Abs(pct)}% this week" });
            }

            // Suspicious activity alerts
            var suspiciousCount = await context.AuditLogs
                .CountAsync(l => l.Action == AuditActions.SuspiciousActivity && l.Timestamp >= now.AddHours(-24));
            if (suspiciousCount > 0)
                insights.Add(new { type = "critical", message = $"{suspiciousCount} suspicious activity alert(s) in the last 24 hours" });

            if (insights.Count == 0)
                insights.Add(new { type = "info", message = "No unusual activity detected. System is operating normally." });

            return Ok(insights);
        }
    }
}
