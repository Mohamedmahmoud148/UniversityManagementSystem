using System.Collections.Generic;
using System.Threading.Tasks;
using NUlid;
using UniversityManagementSystem.Core.DTOs;
using UniversityManagementSystem.Core.Entities;

namespace UniversityManagementSystem.Core.Interfaces
{
    public interface IAssignmentService
    {
        Task<AssignmentDto> CreateAssignmentAsync(CreateAssignmentDto dto, Ulid doctorId);
        Task<List<AssignmentDto>> GetOfferingAssignmentsAsync(Ulid offeringId);
        Task<AssignmentDto> GetByIdAsync(Ulid id);
        Task<AssignmentSubmission> SubmitAsync(Ulid assignmentId, Ulid studentId, string? textAnswer, string? fileUrl, string? storageKey);
        Task<List<SubmissionDto>> GetSubmissionsAsync(Ulid assignmentId, Ulid doctorId);
        Task<SubmissionDto> GradeManuallyAsync(Ulid submissionId, double grade, string? feedback, Ulid doctorId);
        Task<AiGradingResultDto> TriggerAiGradingAsync(Ulid submissionId);
        Task<SubmissionDto?> GetStudentSubmissionAsync(Ulid assignmentId, Ulid studentId);
        Task DeleteAssignmentAsync(Ulid assignmentId, Ulid doctorId);
    }
}
