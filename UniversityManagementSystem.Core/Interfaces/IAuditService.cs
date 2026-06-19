using System.Threading.Tasks;
using NUlid;
using UniversityManagementSystem.Core.DTOs;

namespace UniversityManagementSystem.Core.Interfaces
{
    public interface IAuditService
    {
        /// <summary>Legacy signature — kept for backwards compatibility.</summary>
        Task LogAsync(string actionType, string entityName, string entityId,
            string? oldValues, string? newValues, Ulid? performedByUserId);

        /// <summary>Rich entry — preferred for new code.</summary>
        Task LogAsync(AuditLogEntry entry);
    }
}
