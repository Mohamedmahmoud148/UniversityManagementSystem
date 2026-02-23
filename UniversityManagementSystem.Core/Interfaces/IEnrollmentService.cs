using System.Collections.Generic;
using System.Threading.Tasks;
using UniversityManagementSystem.Core.Entities;
using UniversityManagementSystem.Core.DTOs;

namespace UniversityManagementSystem.Core.Interfaces
{
    public interface IEnrollmentService
    {
        Task EnrollStudentAsync(CreateEnrollmentDto dto);
        Task UnenrollStudentAsync(int enrollmentId);
        Task<IReadOnlyList<Enrollment>> GetStudentEnrollmentsAsync(int studentId);
        Task<IReadOnlyList<Enrollment>> GetEnrollmentsByOfferingIdAsync(int offeringId);
        
        // Admin Override
        Task ReactivateEnrollmentAsync(int enrollmentId);
    }
}
