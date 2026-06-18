using System.Collections.Generic;
using System.Threading.Tasks;
using NUlid;
using UniversityManagementSystem.Core.DTOs.Lecture;

namespace UniversityManagementSystem.Core.Interfaces
{
    public interface ILectureIntelligenceService
    {
        Task<LectureRecordingDto> UploadAsync(Ulid studentId, byte[] audioBytes, string fileName, string mimeType, long fileSize);
        Task<LectureRecordingDto?> GetAsync(Ulid recordingId, Ulid studentId);
        Task<List<LectureRecordingDto>> GetMyRecordingsAsync(Ulid studentId);
        Task<LectureSummaryDto?> GetSummaryAsync(Ulid recordingId, Ulid studentId);
        Task<List<LectureFlashcardDto>> GetFlashcardsAsync(Ulid recordingId, Ulid studentId);
        Task<List<LectureQuizDto>> GetQuizAsync(Ulid recordingId, Ulid studentId);
        Task<string> AskAsync(Ulid recordingId, Ulid studentId, string message);
        Task<LectureDashboardDto> GetDashboardAsync(Ulid studentId);
        Task DeleteAsync(Ulid recordingId, Ulid studentId);

        // Called by background job
        Task ProcessRecordingAsync(Ulid recordingId);
    }
}
