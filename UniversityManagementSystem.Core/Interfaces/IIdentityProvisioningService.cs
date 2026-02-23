using System.Threading.Tasks;
using UniversityManagementSystem.Core.Entities;

namespace UniversityManagementSystem.Core.Interfaces
{
    public interface IIdentityProvisioningService
    {
        Task<string> GenerateStudentIdAsync(int batchId, int departmentId);
        Task<string> GenerateUniversityEmailAsync(string firstName, string lastName, UserRole role);
        Task<string> GenerateStaffIdAsync(int departmentId);
        string GenerateSecurePassword(int length = 12);
    }
}
