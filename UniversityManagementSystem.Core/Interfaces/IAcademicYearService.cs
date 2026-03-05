using System.Collections.Generic;
using System.Threading.Tasks;
using UniversityManagementSystem.Core.DTOs;
using NUlid;

namespace UniversityManagementSystem.Core.Interfaces
{
    public interface IAcademicYearService
    {
        Task<AcademicYearDto> CreateAsync(CreateAcademicYearDto dto);
        Task<AcademicYearDto> UpdateAsync(Ulid id, UpdateAcademicYearDto dto);
        Task DeleteAsync(Ulid id);
        Task<IEnumerable<AcademicYearDto>> GetAllAsync();
        Task ActivateAsync(Ulid id);
    }
}
