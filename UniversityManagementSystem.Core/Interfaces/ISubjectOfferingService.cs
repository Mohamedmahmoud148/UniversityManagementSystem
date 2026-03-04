using System.Collections.Generic;
using System.Threading.Tasks;
using UniversityManagementSystem.Core.DTOs;

namespace UniversityManagementSystem.Core.Interfaces
{
    public interface ISubjectOfferingService
    {
        Task<SubjectOfferingDto> CreateAsync(CreateSubjectOfferingDto dto);
        Task<SubjectOfferingDto?> GetByPublicIdAsync(string publicId);
        Task<IEnumerable<SubjectOfferingDto>> GetBySemesterAsync(int semesterId);
        Task<IEnumerable<SubjectOfferingDto>> GetByDoctorAsync(int systemUserId);
        Task<IEnumerable<SubjectOfferingDto>> GetByStudentAsync(int studentId);
    }
}
