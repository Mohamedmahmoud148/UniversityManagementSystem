using System.Threading.Tasks;
using UniversityManagementSystem.Core.Entities;
using NUlid;

namespace UniversityManagementSystem.Core.Interfaces
{
    public interface IIdentityProvisioningService
    {
        Task<string> GenerateStudentIdAsync(Ulid batchId, Ulid departmentId);
        Task<string> GenerateUniversityEmailAsync(string firstName, string lastName, UserRole role);
        Task<string> GenerateStaffIdAsync(Ulid departmentId);
        string GenerateSecurePassword(int length = 12);
    }
}
