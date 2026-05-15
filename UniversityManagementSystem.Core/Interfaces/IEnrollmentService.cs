using System.Collections.Generic;
using System.Threading.Tasks;
using UniversityManagementSystem.Core.Entities;
using UniversityManagementSystem.Core.DTOs;
using NUlid;

namespace UniversityManagementSystem.Core.Interfaces
{
    public interface IEnrollmentService
    {
        Task EnrollStudentAsync(CreateEnrollmentDto dto);
        Task UnenrollStudentAsync(Ulid enrollmentId);
        Task<IReadOnlyList<Enrollment>> GetStudentEnrollmentsAsync(Ulid studentId);
        Task<IReadOnlyList<Enrollment>> GetEnrollmentsByOfferingIdAsync(Ulid offeringId);

        // Admin Override
        Task ReactivateEnrollmentAsync(Ulid enrollmentId);

        /// <summary>
        /// Finds all SubjectOfferings open for the student's batch/department/group
        /// that they are not already enrolled in, and enrolls them in all of them.
        /// </summary>
        Task<AutoEnrollResultDto> AutoEnrollAsync(Ulid studentId);
    }
}
