using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUlid;
using UniversityManagementSystem.Core.Entities;
using UniversityManagementSystem.Infrastructure.Data;

namespace UniversityManagementSystem.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "SuperAdmin,Admin")]
    public class AuditLogsController(AppDbContext context) : ControllerBase
    {
        // ── GET /api/auditlogs ────────────────────────────────────────────────
        [HttpGet]
        public async Task<IActionResult> GetAll(
            [FromQuery] int page          = 1,
            [FromQuery] int pageSize      = 20,
            [FromQuery] string? entity    = null,
            [FromQuery] string? action    = null,
            [FromQuery] string? userId    = null,
            [FromQuery] string? userName  = null,
            [FromQuery] string? email     = null,
            [FromQuery] string? role      = null,
            [FromQuery] string? severity  = null,
            [FromQuery] string? status    = null,
            [FromQuery] string? search    = null,
            [FromQuery] DateTime? dateFrom = null,
            [FromQuery] DateTime? dateTo   = null,
            [FromQuery] string sortBy     = "timestamp",
            [FromQuery] bool sortDesc     = true)
        {
            if (page < 1) page = 1;
            if (pageSize is < 1 or > 100) pageSize = 20;

            var query = context.AuditLogs.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(entity))
                query = query.Where(l => l.Entity.ToLower() == entity.ToLower());
            if (!string.IsNullOrWhiteSpace(action))
                query = query.Where(l => l.Action.ToLower() == action.ToLower());
            if (!string.IsNullOrWhiteSpace(userId) && Ulid.TryParse(userId, out var uid))
                query = query.Where(l => l.UserId == uid);
            if (!string.IsNullOrWhiteSpace(userName))
                query = query.Where(l => l.UserName != null && l.UserName.ToLower().Contains(userName.ToLower()));
            if (!string.IsNullOrWhiteSpace(email))
                query = query.Where(l => l.Email != null && l.Email.ToLower().Contains(email.ToLower()));
            if (!string.IsNullOrWhiteSpace(role))
                query = query.Where(l => l.Role != null && l.Role.ToLower() == role.ToLower());
            if (!string.IsNullOrWhiteSpace(status))
                query = query.Where(l => l.Status.ToLower() == status.ToLower());
            if (!string.IsNullOrWhiteSpace(severity) && Enum.TryParse<AuditSeverity>(severity, true, out var sev))
                query = query.Where(l => l.Severity == sev);
            if (dateFrom.HasValue)
                query = query.Where(l => l.Timestamp >= dateFrom.Value.ToUniversalTime());
            if (dateTo.HasValue)
                query = query.Where(l => l.Timestamp <= dateTo.Value.ToUniversalTime());
            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(l =>
                    l.Description.Contains(search) ||
                    l.UserName!.Contains(search) ||
                    l.Entity.Contains(search) ||
                    l.Action.Contains(search));

            // Sorting
            query = (sortBy.ToLower(), sortDesc) switch
            {
                ("action",   true)  => query.OrderByDescending(l => l.Action),
                ("action",   false) => query.OrderBy(l => l.Action),
                ("entity",   true)  => query.OrderByDescending(l => l.Entity),
                ("entity",   false) => query.OrderBy(l => l.Entity),
                ("severity", true)  => query.OrderByDescending(l => l.Severity),
                ("severity", false) => query.OrderBy(l => l.Severity),
                (_,          true)  => query.OrderByDescending(l => l.Timestamp),
                (_,          false) => query.OrderBy(l => l.Timestamp),
            };

            var total = await query.CountAsync();

            var logs = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(l => new
                {
                    id            = l.Id.ToString(),
                    timestamp     = l.Timestamp,
                    userId        = l.UserId.HasValue ? l.UserId.Value.ToString() : null,
                    l.UserName,
                    l.Email,
                    l.Role,
                    l.Action,
                    l.Entity,
                    l.EntityId,
                    l.Description,
                    severity      = l.Severity.ToString(),
                    l.Status,
                    l.IpAddress,
                    l.UserAgent,
                    l.Browser,
                    l.Device,
                    l.CorrelationId,
                    l.RequestId,
                    l.DurationMs,
                    l.OldValues,
                    l.NewValues,
                    l.ChangedFields,
                    l.Metadata
                })
                .ToListAsync();

            return Ok(new
            {
                page,
                pageSize,
                total,
                totalPages  = (int)Math.Ceiling(total / (double)pageSize),
                hasNextPage = page * pageSize < total,
                data        = logs
            });
        }

        // ── GET /api/auditlogs/export/csv ─────────────────────────────────────
        [HttpGet("export/csv")]
        public async Task<IActionResult> ExportCsv(
            [FromQuery] string? entity   = null,
            [FromQuery] string? action   = null,
            [FromQuery] string? severity = null,
            [FromQuery] string? status   = null,
            [FromQuery] DateTime? dateFrom = null,
            [FromQuery] DateTime? dateTo   = null)
        {
            var logs = await BuildExportQuery(entity, action, severity, status, dateFrom, dateTo).ToListAsync();

            var sb = new StringBuilder();
            sb.AppendLine("Timestamp,User,Email,Role,Action,Entity,EntityId,Severity,Status,IP,Browser,Device,Description");
            foreach (var l in logs)
                sb.AppendLine($"\"{l.Timestamp:O}\",\"{l.UserName}\",\"{l.Email}\",\"{l.Role}\",\"{l.Action}\",\"{l.Entity}\",\"{l.EntityId}\",\"{l.Severity}\",\"{l.Status}\",\"{l.IpAddress}\",\"{l.Browser}\",\"{l.Device}\",\"{l.Description?.Replace("\"", "\"\"")}\"");

            var bytes = Encoding.UTF8.GetBytes(sb.ToString());
            return File(bytes, "text/csv", $"audit-logs-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv");
        }

        // ── GET /api/auditlogs/export/excel ──────────────────────────────────
        [HttpGet("export/excel")]
        public async Task<IActionResult> ExportExcel(
            [FromQuery] string? entity   = null,
            [FromQuery] string? action   = null,
            [FromQuery] string? severity = null,
            [FromQuery] string? status   = null,
            [FromQuery] DateTime? dateFrom = null,
            [FromQuery] DateTime? dateTo   = null)
        {
            var logs = await BuildExportQuery(entity, action, severity, status, dateFrom, dateTo).ToListAsync();

            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("Audit Logs");

            // Title
            ws.Cell(1, 1).Value = "Audit Logs Export";
            ws.Range(1, 1, 1, 14).Merge();
            ws.Cell(1, 1).Style.Font.Bold = true;
            ws.Cell(1, 1).Style.Font.FontSize = 14;
            ws.Cell(1, 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#1E3A5F");
            ws.Cell(1, 1).Style.Font.FontColor = XLColor.White;
            ws.Cell(1, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            ws.Cell(2, 1).Value = $"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC  •  Total: {logs.Count} records";
            ws.Range(2, 1, 2, 14).Merge();
            ws.Cell(2, 1).Style.Font.Italic = true;
            ws.Cell(2, 1).Style.Font.FontSize = 9;
            ws.Cell(2, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            // Headers
            var headers = new[] { "#", "Timestamp", "User", "Email", "Role", "Action", "Entity", "EntityId", "Severity", "Status", "IP Address", "Browser", "Device", "Description" };
            for (int c = 0; c < headers.Length; c++)
            {
                var cell = ws.Cell(3, c + 1);
                cell.Value = headers[c];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#2E75B6");
                cell.Style.Font.FontColor = XLColor.White;
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            }

            // Data
            for (int i = 0; i < logs.Count; i++)
            {
                var l = logs[i];
                int r = i + 4;
                ws.Cell(r, 1).Value  = i + 1;
                ws.Cell(r, 2).Value  = l.Timestamp.ToString("yyyy-MM-dd HH:mm:ss");
                ws.Cell(r, 3).Value  = l.UserName ?? "";
                ws.Cell(r, 4).Value  = l.Email ?? "";
                ws.Cell(r, 5).Value  = l.Role ?? "";
                ws.Cell(r, 6).Value  = l.Action;
                ws.Cell(r, 7).Value  = l.Entity;
                ws.Cell(r, 8).Value  = l.EntityId;
                ws.Cell(r, 9).Value  = l.Severity.ToString();
                ws.Cell(r, 10).Value = l.Status;
                ws.Cell(r, 11).Value = l.IpAddress ?? "";
                ws.Cell(r, 12).Value = l.Browser ?? "";
                ws.Cell(r, 13).Value = l.Device ?? "";
                ws.Cell(r, 14).Value = l.Description;

                if (i % 2 == 0) ws.Range(r, 1, r, 14).Style.Fill.BackgroundColor = XLColor.FromHtml("#EBF3FB");

                // Color-code severity
                var severityColor = l.Severity switch
                {
                    AuditSeverity.Critical => XLColor.FromHtml("#C00000"),
                    AuditSeverity.Security => XLColor.FromHtml("#7B0099"),
                    AuditSeverity.Warning  => XLColor.FromHtml("#8B6914"),
                    AuditSeverity.Error    => XLColor.FromHtml("#CC3300"),
                    _                      => XLColor.FromHtml("#1E6B3E")
                };
                ws.Cell(r, 9).Style.Font.FontColor = severityColor;
                ws.Cell(r, 9).Style.Font.Bold = true;
            }

            // Column widths
            int[] widths = [5, 20, 22, 28, 12, 20, 16, 28, 12, 10, 16, 12, 12, 40];
            for (int c = 0; c < widths.Length; c++) ws.Column(c + 1).Width = widths[c];
            ws.SheetView.Freeze(3, 0);

            using var ms = new MemoryStream();
            workbook.SaveAs(ms);
            return File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"audit-logs-{DateTime.UtcNow:yyyyMMdd-HHmmss}.xlsx");
        }

        // ── GET /api/auditlogs/export/pdf ─────────────────────────────────────
        [HttpGet("export/pdf")]
        public async Task<IActionResult> ExportPdf(
            [FromQuery] string? entity   = null,
            [FromQuery] string? action   = null,
            [FromQuery] string? severity = null,
            [FromQuery] string? status   = null,
            [FromQuery] DateTime? dateFrom = null,
            [FromQuery] DateTime? dateTo   = null)
        {
            var logs = await BuildExportQuery(entity, action, severity, status, dateFrom, dateTo)
                .Take(500).ToListAsync();

            var sb = new StringBuilder();
            sb.AppendLine("<!DOCTYPE html><html><head><meta charset='utf-8'>");
            sb.AppendLine("<style>body{font-family:Arial,sans-serif;font-size:11px;}");
            sb.AppendLine("h1{color:#1E3A5F;font-size:16px;}");
            sb.AppendLine("table{width:100%;border-collapse:collapse;}");
            sb.AppendLine("th{background:#2E75B6;color:white;padding:6px;text-align:left;}");
            sb.AppendLine("td{padding:4px 6px;border-bottom:1px solid #ddd;}");
            sb.AppendLine("tr:nth-child(even){background:#EBF3FB;}");
            sb.AppendLine(".Critical,.Security{color:#C00000;font-weight:bold;}");
            sb.AppendLine(".Warning{color:#8B6914;font-weight:bold;}");
            sb.AppendLine("@media print{@page{margin:1cm;}}</style></head><body>");
            sb.AppendLine($"<h1>Audit Logs — Export</h1>");
            sb.AppendLine($"<p>Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC | Total: {logs.Count} records</p>");
            sb.AppendLine("<table><thead><tr><th>#</th><th>Timestamp</th><th>User</th><th>Role</th><th>Action</th><th>Entity</th><th>Severity</th><th>Status</th><th>IP</th><th>Description</th></tr></thead><tbody>");

            for (int i = 0; i < logs.Count; i++)
            {
                var l = logs[i];
                var sev = l.Severity.ToString();
                sb.AppendLine($"<tr><td>{i + 1}</td><td>{l.Timestamp:yyyy-MM-dd HH:mm:ss}</td><td>{l.UserName ?? "-"}</td><td>{l.Role ?? "-"}</td><td>{l.Action}</td><td>{l.Entity}</td><td class='{sev}'>{sev}</td><td>{l.Status}</td><td>{l.IpAddress ?? "-"}</td><td>{System.Net.WebUtility.HtmlEncode(l.Description)}</td></tr>");
            }

            sb.AppendLine("</tbody></table></body></html>");

            var bytes = Encoding.UTF8.GetBytes(sb.ToString());
            return File(bytes, "text/html", $"audit-logs-{DateTime.UtcNow:yyyyMMdd-HHmmss}.html");
        }

        // ── Shared filter builder ─────────────────────────────────────────────
        private IQueryable<AuditLog> BuildExportQuery(
            string? entity, string? action, string? severity, string? status,
            DateTime? dateFrom, DateTime? dateTo)
        {
            var query = context.AuditLogs.AsNoTracking().OrderByDescending(l => l.Timestamp).AsQueryable();

            if (!string.IsNullOrWhiteSpace(entity))
                query = query.Where(l => l.Entity.ToLower() == entity.ToLower());
            if (!string.IsNullOrWhiteSpace(action))
                query = query.Where(l => l.Action.ToLower() == action.ToLower());
            if (!string.IsNullOrWhiteSpace(status))
                query = query.Where(l => l.Status.ToLower() == status.ToLower());
            if (!string.IsNullOrWhiteSpace(severity) && Enum.TryParse<AuditSeverity>(severity, true, out var sev))
                query = query.Where(l => l.Severity == sev);
            if (dateFrom.HasValue)
                query = query.Where(l => l.Timestamp >= dateFrom.Value.ToUniversalTime());
            if (dateTo.HasValue)
                query = query.Where(l => l.Timestamp <= dateTo.Value.ToUniversalTime());

            return query;
        }
    }
}
