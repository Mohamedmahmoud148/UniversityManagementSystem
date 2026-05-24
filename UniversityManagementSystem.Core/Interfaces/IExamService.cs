using System.Collections.Generic;
using System.Threading.Tasks;
using UniversityManagementSystem.Core.DTOs;
using NUlid;

namespace UniversityManagementSystem.Core.Interfaces
{
    public interface IExamService
    {
        Task<ExamDto> CreateExamAsync(Ulid subjectOfferingId, CreateExamDto dto, Ulid doctorId);
        Task<ExamDto> GetExamByIdAsync(Ulid examId, Ulid userId, string userRole);
        Task<ExamDto?> GetExamByCodeAsync(string code, Ulid userId, string userRole);
        Task<Ulid> SubmitExamAsync(Ulid examId, Ulid studentId, ExamSubmissionDto submissionDto);
        Task<ExamDto> GenerateAiExamAsync(CreateAiExamRequest request, Ulid doctorId);
        Task<ExamDto> UploadFileExamAsync(Ulid subjectOfferingId, Microsoft.AspNetCore.Http.IFormFile file, Ulid doctorId);

        // Queries
        Task<IEnumerable<ExamDto>> GetExamsByDoctorAsync(Ulid doctorId);
        Task<IEnumerable<ExamDto>> GetExamsByOfferingAsync(Ulid offeringId, Ulid doctorId);
        Task<IEnumerable<ExamSubmissionResponseDto>> GetExamSubmissionsAsync(Ulid examId, Ulid doctorId);
        Task<IEnumerable<ExamDto>> GetStudentEnrolledExamsAsync(Ulid studentId);
        Task<StudentSubmissionResultDto?> GetStudentSubmissionAsync(Ulid examId, Ulid studentId);

        // Randomized exam — returns the student's personal question subset
        Task<ExamDto> GetStudentVariantAsync(Ulid examId, Ulid studentId);

        // Submission detail + per-question grading
        Task<SubmissionDetailDto> GetSubmissionDetailAsync(Ulid submissionId, Ulid doctorId);
        Task GradeQuestionAsync(Ulid submissionId, GradeQuestionDto dto, Ulid doctorId);

        // Grading
        Task GradeSubmissionAsync(GradeSubmissionDto dto, Ulid doctorId);
        Task<int> AutoGradeExamAsync(Ulid examId, Ulid doctorId); // Returns count of graded submissions

        // Session management
        Task<SaveProgressResponseDto> SaveProgressAsync(Ulid examId, Ulid studentId, SaveProgressDto dto);
        Task<ExamSessionDto> GetExamSessionAsync(Ulid examId, Ulid studentId);

        // Analytics
        Task<ExamAnalyticsDto> GetExamAnalyticsAsync(Ulid examId, Ulid doctorId);

        // Admin Overrides
        Task<ExamDto> UpdateExamAsync(Ulid examId, UpdateExamDto dto);
        Task ArchiveExamAsync(Ulid examId);
        Task RestoreExamAsync(Ulid examId);
        Task DeleteExamAsync(Ulid examId);
    }
}
