using System.Collections.Generic;
using System.Threading.Tasks;
using UniversityManagementSystem.Core.DTOs;
using NUlid;

namespace UniversityManagementSystem.Core.Interfaces
{
    public interface IFileService
    {
        Task<FileStatusDto> UploadFileAsync(Ulid userId, UploadFileDto fileDto);
        Task<FileStatusDto> UploadFileStreamAsync(Ulid userId, Stream stream, string fileName, string contentType, long fileLength);
        Task<IEnumerable<FileStatusDto>> GetUserFilesAsync(Ulid userId);
        Task<FileStatusDto?> GetFileStatusAsync(Ulid fileId);
        Task RenameFileAsync(Ulid fileId, RenameFileDto dto);
        Task DeleteFileAsync(Ulid fileId);
    }
}
