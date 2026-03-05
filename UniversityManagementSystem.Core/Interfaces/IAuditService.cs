using System.Threading.Tasks;
using NUlid;

namespace UniversityManagementSystem.Core.Interfaces
{
    public interface IAuditService
    {
        Task LogAsync(string actionType, string entityName, string entityId, string? oldValues, string? newValues, Ulid? performedByUserId);
    }
}
