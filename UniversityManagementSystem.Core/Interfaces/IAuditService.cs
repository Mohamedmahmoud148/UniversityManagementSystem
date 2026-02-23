using System.Threading.Tasks;

namespace UniversityManagementSystem.Core.Interfaces
{
    public interface IAuditService
    {
        Task LogAsync(string actionType, string entityName, string entityId, string? oldValues, string? newValues, int? performedByUserId);
    }
}
