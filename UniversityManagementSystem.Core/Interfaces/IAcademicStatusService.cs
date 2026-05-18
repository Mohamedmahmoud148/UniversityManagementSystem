using System.Threading.Tasks;
using NUlid;
using UniversityManagementSystem.Core.Entities;

namespace UniversityManagementSystem.Core.Interfaces
{
    public interface IAcademicStatusService
    {
        /// <summary>
        /// Recalculates and persists GPA + academic standing for a student.
        /// Call this after every grade finalization.
        /// </summary>
        Task RecalculateAsync(Ulid studentId);

        /// <summary>
        /// Gets or creates the StudentAcademicStatus for a student.
        /// </summary>
        Task<StudentAcademicStatus> GetOrCreateAsync(Ulid studentId);

        /// <summary>
        /// Loads the current policy (department-specific or global fallback).
        /// </summary>
        Task<AcademicPolicy> GetPolicyAsync(Ulid? departmentId = null);
    }
}
