using System.Collections.Generic;
using System.Threading.Tasks;
using UniversityManagementSystem.Core.DTOs;
using UniversityManagementSystem.Core.Entities;

namespace UniversityManagementSystem.Core.Interfaces
{
    public interface IRegulationService
    {
        Task<IEnumerable<Regulation>> GetAllAsync();
        Task<IEnumerable<Regulation>> GetActiveAsync();
        Task<Regulation> CreateAsync(Regulation regulation);
        Task UpdateAsync(int id, Regulation regulation);
        Task DeleteAsync(int id);
    }
}
