using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using UniversityManagementSystem.Core.Constants;
using UniversityManagementSystem.Core.Entities;
using UniversityManagementSystem.Infrastructure.Data;

namespace UniversityManagementSystem.Infrastructure.Services
{
    public class SuspiciousActivityDetector(AppDbContext context, ILogger<SuspiciousActivityDetector> logger)
    {
        public async Task CheckAsync(AuditLog log)
        {
            try
            {
                var now = DateTime.UtcNow;

                // Rule 1: 5+ failed logins from same IP in 2 minutes
                if (log.Action == AuditActions.FailedLogin && !string.IsNullOrEmpty(log.IpAddress))
                {
                    var cutoff = now.AddMinutes(-2);
                    var count = await context.AuditLogs
                        .CountAsync(l => l.Action == AuditActions.FailedLogin
                                      && l.IpAddress == log.IpAddress
                                      && l.Timestamp >= cutoff);
                    if (count >= 5)
                    {
                        await WriteAlertAsync(
                            $"5+ failed logins from IP {log.IpAddress} in 2 minutes",
                            AuditSeverity.Security,
                            log.IpAddress, log.UserId, log.UserName);
                    }
                }

                // Rule 2: 10+ grade modifications in 5 minutes by same user
                if ((log.Action == AuditActions.UpdateGrade || log.Action == AuditActions.ImportGrades)
                    && log.UserId.HasValue)
                {
                    var cutoff = now.AddMinutes(-5);
                    var count = await context.AuditLogs
                        .CountAsync(l => (l.Action == AuditActions.UpdateGrade || l.Action == AuditActions.ImportGrades)
                                      && l.UserId == log.UserId
                                      && l.Timestamp >= cutoff);
                    if (count >= 10)
                    {
                        await WriteAlertAsync(
                            $"Bulk grade modifications: {count} actions in 5 minutes by {log.UserName}",
                            AuditSeverity.Warning,
                            log.IpAddress, log.UserId, log.UserName);
                    }
                }

                // Rule 3: 5+ soft-deletes in 1 minute by same user
                if (log.Action == AuditActions.SoftDelete && log.UserId.HasValue)
                {
                    var cutoff = now.AddMinutes(-1);
                    var count = await context.AuditLogs
                        .CountAsync(l => l.Action == AuditActions.SoftDelete
                                      && l.UserId == log.UserId
                                      && l.Timestamp >= cutoff);
                    if (count >= 5)
                    {
                        await WriteAlertAsync(
                            $"Bulk deletions: {count} deletes in 1 minute by {log.UserName}",
                            AuditSeverity.Critical,
                            log.IpAddress, log.UserId, log.UserName);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "SuspiciousActivityDetector failed silently");
            }
        }

        private async Task WriteAlertAsync(string description, AuditSeverity severity,
            string? ip, NUlid.Ulid? userId, string? userName)
        {
            // Avoid duplicate alerts: skip if same description written in last 10 min
            var recent = await context.AuditLogs
                .AnyAsync(l => l.Action == AuditActions.SuspiciousActivity
                             && l.Description == description
                             && l.Timestamp >= DateTime.UtcNow.AddMinutes(-10));
            if (recent) return;

            context.AuditLogs.Add(new AuditLog
            {
                Action      = AuditActions.SuspiciousActivity,
                Entity      = "Security",
                EntityId    = userId?.ToString() ?? "unknown",
                Description = description,
                Severity    = severity,
                Status      = "Alert",
                IpAddress   = ip,
                UserId      = userId,
                UserName    = userName
            });
            await context.SaveChangesAsync();
            logger.LogWarning("Suspicious activity detected: {Description}", description);
        }
    }
}
