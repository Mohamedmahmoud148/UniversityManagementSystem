using System;
using System.Threading.Tasks;
using UniversityManagementSystem.Core.Entities;
using UniversityManagementSystem.Core.Interfaces;
using UniversityManagementSystem.Infrastructure.Data;
using NUlid;

namespace UniversityManagementSystem.Infrastructure.Services
{
    public class AuditService(AppDbContext context) : IAuditService
    {
        private readonly AppDbContext _context = context;

        public async Task LogAsync(string actionType, string entityName, string entityId, string? oldValues, string? newValues, Ulid? performedByUserId)
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
    }
}
