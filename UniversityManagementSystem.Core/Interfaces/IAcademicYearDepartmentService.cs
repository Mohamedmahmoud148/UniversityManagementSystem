using System.Collections.Generic;
using System.Threading.Tasks;
using UniversityManagementSystem.Core.DTOs;
using NUlid;

namespace UniversityManagementSystem.Core.Interfaces
{
    public interface IAcademicYearDepartmentService
    {
        /// <summary>
        /// Returns only ACTIVE department mappings for the given year.
        /// Used by students/doctors to see which departments are open for that year.
        /// </summary>
        Task<IEnumerable<AcademicYearDepartmentDto>> GetActiveDepartmentsForYearAsync(Ulid yearId);

        /// <summary>
        /// Returns ALL mappings (active + inactive) for admin management views.
        /// </summary>
        Task<IEnumerable<AcademicYearDepartmentDto>> GetAllMappingsForYearAsync(Ulid yearId);

        /// <summary>
        /// Assigns a department to an academic year.
        /// Enforces: Department.CollegeId == AcademicYear.CollegeId.
        /// Enforces: no duplicate mapping.
        /// </summary>
        Task<AcademicYearDepartmentDto> AssignDepartmentAsync(Ulid yearId, AssignDepartmentToYearDto dto);

        /// <summary>Toggles the IsActive flag on an existing mapping by mapping ID.</summary>
        Task UpdateMappingAsync(Ulid mappingId, bool isActive);

        /// <summary>Permanently removes a mapping (hard delete — it's a config record).</summary>
        Task RemoveMappingAsync(Ulid mappingId);

        /// <summary>
        /// Assigns a department to ALL academic years within the same college.
        /// Skips years that already have the mapping. Returns only the newly created mappings.
        /// </summary>
        Task<IEnumerable<AcademicYearDepartmentDto>> AssignDepartmentToAllYearsAsync(
            Ulid sourceYearId, Ulid departmentId, bool isActive);
    }
}
