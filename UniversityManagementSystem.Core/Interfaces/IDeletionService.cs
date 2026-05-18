using System.Threading.Tasks;
using NUlid;
using UniversityManagementSystem.Core.DTOs;

namespace UniversityManagementSystem.Core.Interfaces
{
    public interface IDeletionService
    {
        /// <summary>
        /// Analyze impact of deleting an entity without executing the delete.
        /// Returns full dependency tree, risk level, warnings, and confirmation requirements.
        /// </summary>
        Task<DeleteAnalysisResponseDto> AnalyzeAsync(string entityName, Ulid entityId);

        /// <summary>
        /// Execute deletion after all confirmation requirements are satisfied.
        /// Validates confirmation phrase, password, and second-admin token if required.
        /// </summary>
        Task<DeleteExecutionResponseDto> ExecuteAsync(DeleteExecutionRequestDto request, Ulid performedByUserId);
    }
}
