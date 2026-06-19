using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using NUlid;
using UniversityManagementSystem.Core.DTOs;
using UniversityManagementSystem.Core.Entities;
using UniversityManagementSystem.Core.Interfaces;
using UniversityManagementSystem.Infrastructure.Data;
using UniversityManagementSystem.Api.Hubs;

namespace UniversityManagementSystem.Infrastructure.Services
{
    public class AuditService(
        AppDbContext context,
        ILogger<AuditService> logger,
        IHubContext<AuditHub> hubContext,
        SuspiciousActivityDetector detector) : IAuditService
    {
        // ── Legacy signature — backwards compat ───────────────────────────────
        public Task LogAsync(string actionType, string entityName, string entityId,
            string? oldValues, string? newValues, Ulid? performedByUserId)
            => LogAsync(new AuditLogEntry
            {
                Action     = actionType,
                Entity     = entityName,
                EntityId   = entityId,
                OldValues  = oldValues,
                NewValues  = newValues,
                UserId     = performedByUserId,
                Severity   = AuditSeverity.Info,
                Status     = "Success",
                Description = $"{actionType} on {entityName}"
            });

        // ── Rich entry — primary path ─────────────────────────────────────────
        public async Task LogAsync(AuditLogEntry entry)
        {
            try
            {
                var log = new AuditLog
                {
                    Action        = entry.Action,
                    Entity        = entry.Entity,
                    EntityId      = entry.EntityId,
                    Description   = entry.Description,
                    Severity      = entry.Severity,
                    Status        = entry.Status,
                    UserId        = entry.UserId,
                    UserName      = entry.UserName,
                    Email         = entry.Email,
                    Role          = entry.Role,
                    IpAddress     = entry.IpAddress,
                    UserAgent     = entry.UserAgent,
                    Browser       = entry.Browser,
                    Device        = entry.Device,
                    CorrelationId = entry.CorrelationId,
                    OldValues     = entry.OldValues,
                    NewValues     = entry.NewValues,
                    ChangedFields = entry.ChangedFields,
                    Metadata      = entry.Metadata,
                    DurationMs    = entry.DurationMs,
                    Timestamp     = DateTime.UtcNow
                };

                context.AuditLogs.Add(log);
                await context.SaveChangesAsync();

                // Push real-time event to admin clients
                await hubContext.Clients.Group("admins").SendAsync("AuditCreated", new
                {
                    id          = log.Id.ToString(),
                    action      = log.Action,
                    entity      = log.Entity,
                    entityId    = log.EntityId,
                    userName    = log.UserName,
                    role        = log.Role,
                    severity    = log.Severity.ToString(),
                    status      = log.Status,
                    description = log.Description,
                    ipAddress   = log.IpAddress,
                    timestamp   = log.Timestamp
                });

                // Run suspicious-activity checks (never throws)
                await detector.CheckAsync(log);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Audit log write failed: {Action} on {Entity}/{Id}",
                    entry.Action, entry.Entity, entry.EntityId);
            }
        }
    }
}
