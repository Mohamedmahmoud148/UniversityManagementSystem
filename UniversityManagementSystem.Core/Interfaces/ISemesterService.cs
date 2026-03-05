using System.Collections.Generic;
using System.Threading.Tasks;
using UniversityManagementSystem.Core.DTOs;
using NUlid;

namespace UniversityManagementSystem.Core.Interfaces
{
    public interface ISemesterService
    {
        Task<SemesterDto> CreateAsync(CreateSemesterDto dto);
        Task<SemesterDto> UpdateAsync(Ulid id, UpdateSemesterDto dto);
        Task DeleteAsync(Ulid id);
        Task<IEnumerable<SemesterDto>> GetByAcademicYearAsync(Ulid academicYearId);
    }
}
