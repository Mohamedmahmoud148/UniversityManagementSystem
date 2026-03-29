using System.Collections.Generic;
using System.Threading.Tasks;
using NUlid;
using UniversityManagementSystem.Core.DTOs;

namespace UniversityManagementSystem.Core.Interfaces
{
    public interface IAdminService
    {
        Task<IEnumerable<AdminDto>> GetAllAdminsAsync();
        Task<AdminDto?> GetAdminByIdAsync(Ulid id);
        Task UpdateAdminAsync(Ulid id, UpdateAdminDto dto);
        Task DeleteAdminAsync(Ulid id);
        Task ToggleAdminStatusAsync(Ulid id, bool isActive);
    }
}
