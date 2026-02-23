using System.Collections.Generic;
using System.Threading.Tasks;
using UniversityManagementSystem.Core.DTOs;

namespace UniversityManagementSystem.Core.Interfaces
{
    public interface IAcademicYearService
    {
        Task<AcademicYearDto> CreateAsync(CreateAcademicYearDto dto);
        Task<AcademicYearDto> UpdateAsync(int id, UpdateAcademicYearDto dto);
        Task DeleteAsync(int id);
        Task<IEnumerable<AcademicYearDto>> GetAllAsync();
        Task ActivateAsync(int id);
    }
}
