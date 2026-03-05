using System.Collections.Generic;
using System.Threading.Tasks;
using UniversityManagementSystem.Core.DTOs;
using NUlid;

namespace UniversityManagementSystem.Core.Interfaces
{
    public interface IAttendanceService
    {
        Task<QrCodeResponseDto> CreateSessionAsync(CreateAttendanceSessionDto dto, Ulid profileId, string role);
        Task<bool> RecordAttendanceAsync(Ulid studentId, RecordAttendanceDto dto);
        Task<IEnumerable<RecordAttendanceDto>> GetStudentAttendanceAsync(Ulid studentId, Ulid subjectId);

        // Admin Override
        Task<AttendanceResponseDto> GetByIdAsync(Ulid sessionId, Ulid studentId);
        Task UpdateAttendanceAsync(Ulid sessionId, Ulid studentId, bool isPresent);
        Task DeleteAttendanceAsync(Ulid sessionId, Ulid studentId);
    }
}
