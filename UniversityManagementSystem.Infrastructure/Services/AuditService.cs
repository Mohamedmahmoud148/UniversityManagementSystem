using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using UniversityManagementSystem.Core.Entities;
using UniversityManagementSystem.Core.Interfaces;
using UniversityManagementSystem.Infrastructure.Data;
using NUlid;

namespace UniversityManagementSystem.Infrastructure.Services
{
    public class AuditService(AppDbContext context, ILogger<AuditService> logger) : IAuditService
    {
        private readonly AppDbContext _context = context;
        private readonly ILogger<AuditService> _logger = logger;

        public async Task LogAsync(string actionType, string entityName, string entityId, string? oldValues, string? newValues, Ulid? performedByUserId)
        {
            try
            {
                var log = new AuditLog
                {
                    ActionType = actionType,
                    EntityName = entityName,
                    EntityId = entityId,
                    OldValues = oldValues,
                    NewValues = newValues,
                    PerformedByUserId = performedByUserId,
                    PerformedAt = DateTime.UtcNow
                };

                _context.Set<AuditLog>().Add(log);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                // Audit log failures must never break the main business operation.
                _logger.LogError(ex, "Failed to write audit log: {Action} on {Entity}/{Id}", actionType, entityName, entityId);
            }
        }
    }
}
