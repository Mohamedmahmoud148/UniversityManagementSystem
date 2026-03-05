using System.Collections.Generic;
using System.Threading.Tasks;
using UniversityManagementSystem.Core.DTOs;
using NUlid;

namespace UniversityManagementSystem.Core.Interfaces
{
    public interface ISubjectOfferingService
    {
        Task<SubjectOfferingDto> CreateAsync(CreateSubjectOfferingDto dto);
        Task<SubjectOfferingDto?> GetByCodeAsync(string code);
        Task<IEnumerable<SubjectOfferingDto>> GetBySemesterAsync(Ulid semesterId);
        Task<IEnumerable<SubjectOfferingDto>> GetByDoctorAsync(Ulid systemUserId);
        Task<IEnumerable<SubjectOfferingDto>> GetByStudentAsync(Ulid studentId);
    }
}
