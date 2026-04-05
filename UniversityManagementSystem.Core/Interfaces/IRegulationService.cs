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
        Task<Regulation> CreateAsync(Regulation regulation);
        Task UpdateAsync(Ulid id, Regulation regulation);
        Task DeleteAsync(Ulid id);
    }
}
