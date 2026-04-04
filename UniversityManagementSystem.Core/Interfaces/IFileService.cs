using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using UniversityManagementSystem.Core.DTOs;
using NUlid;

namespace UniversityManagementSystem.Core.Interfaces
{
    public interface IFileService
    {
        /// <summary>
        /// Uploads an <see cref="IFormFile"/> and returns a full response DTO
        /// including the new FileId and a 60-min signed URL.
        /// </summary>
        Task<FileUploadResponseDto> UploadFormFileAsync(Ulid userId, IFormFile file);

        /// <summary>
        /// Uploads a raw stream (used internally, e.g. from Excel import jobs).
        /// </summary>
        Task<Ulid> UploadFileStreamAsync(Ulid userId, Stream stream, string fileName, string contentType, long fileLength);

        Task<IEnumerable<FileStatusDto>> GetUserFilesAsync(Ulid userId);
        Task<FileStatusDto?> GetFileStatusAsync(Ulid fileId);
        Task RenameFileAsync(Ulid fileId, RenameFileDto dto);
        Task DeleteFileAsync(Ulid fileId);
    }
}
