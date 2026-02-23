using System.Collections.Generic;
using System.Threading.Tasks;
using UniversityManagementSystem.Core.DTOs;

namespace UniversityManagementSystem.Core.Interfaces
{
    public interface ISemesterService
    {
        Task<SemesterDto> CreateAsync(CreateSemesterDto dto);
        Task<SemesterDto> UpdateAsync(int id, UpdateSemesterDto dto);
        Task DeleteAsync(int id);
        Task<IEnumerable<SemesterDto>> GetByAcademicYearAsync(int academicYearId);
    }
}
