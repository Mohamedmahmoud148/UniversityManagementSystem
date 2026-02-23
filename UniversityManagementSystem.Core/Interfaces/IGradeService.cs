using System.Threading.Tasks;

namespace UniversityManagementSystem.Core.Interfaces
{
    public interface IGradeService
    {
        Task<int> CalculateGradesForOfferingAsync(int offeringId, int doctorId);
        Task<UniversityManagementSystem.Core.DTOs.StudentGpaDto> CalculateStudentGpaAsync(int studentId);
        
        // Admin Overrides
        Task<UniversityManagementSystem.Core.DTOs.GradeDto> UpdateGradeAsync(int gradeId, UniversityManagementSystem.Core.DTOs.UpdateGradeDto dto);
        Task RecalculateStudentGradeAsync(int gradeId);
        Task InvalidateGradeAsync(int gradeId);
    }
}
