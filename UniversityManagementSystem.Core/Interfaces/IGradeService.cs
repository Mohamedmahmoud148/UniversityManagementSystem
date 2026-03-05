using System.Threading.Tasks;
using NUlid;

namespace UniversityManagementSystem.Core.Interfaces
{
    public interface IGradeService
    {
        Task<int> CalculateGradesForOfferingAsync(Ulid offeringId, Ulid doctorId);
        Task<UniversityManagementSystem.Core.DTOs.StudentGpaDto> CalculateStudentGpaAsync(Ulid studentId);

        // Admin Overrides
        Task<UniversityManagementSystem.Core.DTOs.GradeDto> UpdateGradeAsync(Ulid gradeId, UniversityManagementSystem.Core.DTOs.UpdateGradeDto dto);
        Task RecalculateStudentGradeAsync(Ulid gradeId);
        Task InvalidateGradeAsync(Ulid gradeId);
    }
}
