using System.Threading.Tasks;
using NUlid;
using UniversityManagementSystem.Core.DTOs;

namespace UniversityManagementSystem.Core.Interfaces
{
    public interface IProctoringService
    {
        Task RecordEventAsync(RecordProctoringEventDto dto, Ulid studentId);
        Task<ProctoringReportDto> GetReportAsync(Ulid submissionId);
        Task<System.Collections.Generic.List<ProctoringStudentSummaryDto>> GetExamSummaryAsync(Ulid examId);
        Task FlagSubmissionAsync(FlagSubmissionDto dto);
    }
}
