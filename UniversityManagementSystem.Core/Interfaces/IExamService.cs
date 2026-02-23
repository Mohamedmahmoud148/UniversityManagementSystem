using System.Collections.Generic;
using System.Threading.Tasks;
using UniversityManagementSystem.Core.DTOs;

namespace UniversityManagementSystem.Core.Interfaces
{
    public interface IExamService
    {
        Task<ExamDto> CreateExamAsync(int subjectOfferingId, CreateExamDto dto, int doctorId);
        Task<ExamDto> GetExamByIdAsync(int examId, int userId, string userRole);
        Task<int> SubmitExamAsync(int examId, int studentId, ExamSubmissionDto submissionDto);

        // Queries
        Task<IEnumerable<ExamDto>> GetExamsByDoctorAsync(int doctorId);
        Task<IEnumerable<ExamDto>> GetExamsByOfferingAsync(int offeringId, int doctorId);
        Task<IEnumerable<ExamSubmissionResponseDto>> GetExamSubmissionsAsync(int examId, int doctorId);
        Task<IEnumerable<ExamDto>> GetStudentEnrolledExamsAsync(int studentId);
        Task<ExamSubmissionResponseDto?> GetStudentSubmissionAsync(int examId, int studentId);

        // Grading
        Task GradeSubmissionAsync(GradeSubmissionDto dto, int doctorId);
        Task<int> AutoGradeExamAsync(int examId, int doctorId); // Returns count of graded submissions

        // Admin Overrides
        Task<ExamDto> UpdateExamAsync(int examId, UpdateExamDto dto);
        Task ArchiveExamAsync(int examId);
        Task RestoreExamAsync(int examId);
        Task DeleteExamAsync(int examId);
    }
}
