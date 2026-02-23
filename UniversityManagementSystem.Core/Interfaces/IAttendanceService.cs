using System.Collections.Generic;
using System.Threading.Tasks;
using UniversityManagementSystem.Core.DTOs;

namespace UniversityManagementSystem.Core.Interfaces
{
    public interface IAttendanceService
    {
        Task<QrCodeResponseDto> CreateSessionAsync(CreateAttendanceSessionDto dto, int profileId, string role);
        Task<bool> RecordAttendanceAsync(int studentId, RecordAttendanceDto dto);
        Task<IEnumerable<RecordAttendanceDto>> GetStudentAttendanceAsync(int studentId, int subjectId);
        
        // Admin Override
        Task<AttendanceResponseDto> GetByIdAsync(int sessionId, int studentId);
        Task UpdateAttendanceAsync(int sessionId, int studentId, bool isPresent);
        Task DeleteAttendanceAsync(int sessionId, int studentId);
    }
}
