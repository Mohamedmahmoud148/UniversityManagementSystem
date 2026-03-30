using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using NUlid;
using UniversityManagementSystem.Core.DTOs;

namespace UniversityManagementSystem.Core.Interfaces
{
    public interface IStudentFileService
    {
        /// <summary>
        /// Uploads a file to the "student-files/" R2 folder, extracts text
        /// for PDF/TXT files, and saves the record to the database.
        /// </summary>
        Task<StudentFileUploadResponseDto> UploadAsync(Ulid studentId, IFormFile file);

        /// <summary>Returns all file records belonging to the given student.</summary>
        Task<IEnumerable<StudentFileDto>> GetMyFilesAsync(Ulid studentId);

        /// <summary>
        /// Returns the extracted text if available; otherwise returns the
        /// signed URL (60 min) so the AI service can fetch it.
        /// Also verifies that the file belongs to the given student.
        /// </summary>
        Task<(string content, bool isText)> GetFileContentForAiAsync(Ulid fileId, Ulid studentId);
    }
}
