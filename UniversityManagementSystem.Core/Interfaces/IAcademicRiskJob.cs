using System.Collections.Generic;
using System.Threading.Tasks;
using NUlid;
using UniversityManagementSystem.Core.DTOs;

namespace UniversityManagementSystem.Core.Interfaces
{
    public interface IAcademicRiskJob
    {
        Task RunDailyRiskAnalysisAsync();
        Task<List<StudentRiskDto>> GetAtRiskStudentsAsync(Ulid offeringId);
    }
}
