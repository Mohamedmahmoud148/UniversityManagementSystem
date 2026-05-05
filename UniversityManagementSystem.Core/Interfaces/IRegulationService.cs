using System.Collections.Generic;
using System.Threading.Tasks;
using UniversityManagementSystem.Core.DTOs;
using UniversityManagementSystem.Core.Entities;
using NUlid;

namespace UniversityManagementSystem.Core.Interfaces
{
    public interface IRegulationService
    {
        Task<IEnumerable<Regulation>> GetAllAsync();
        Task<IEnumerable<Regulation>> GetActiveAsync();
        Task<Regulation?> GetByCodeAsync(string code);    // NEW: Admin code-based route support
        Task<IEnumerable<Regulation>> GetByDepartmentAsync(Ulid departmentId);
        Task<Regulation?> GetForStudentAsync(Ulid studentId);

        Task<Regulation> CreateWithSubjectsAsync(Regulation regulation, IEnumerable<RegulationSubject> subjects);
        Task UpdateWithSubjectsAsync(Ulid id, Regulation regulation, IEnumerable<RegulationSubject>? subjects);

        Task<Regulation> CreateAsync(Regulation regulation);
        Task UpdateAsync(Ulid id, Regulation regulation);
        Task DeleteAsync(Ulid id);
    }
}
