using System.Collections.Generic;
using System.Threading.Tasks;
using UniversityManagementSystem.Core.DTOs;

namespace UniversityManagementSystem.Core.Interfaces
{
    public interface IFileService
    {
        Task<FileStatusDto> UploadFileAsync(int userId, UploadFileDto fileDto);
        Task<FileStatusDto> UploadFileStreamAsync(int userId, Stream stream, string fileName, string contentType, long fileLength);
        Task<IEnumerable<FileStatusDto>> GetUserFilesAsync(int userId);
        Task<FileStatusDto?> GetFileStatusAsync(int fileId);
        Task RenameFileAsync(int fileId, RenameFileDto dto);
        Task DeleteFileAsync(int fileId);
    }
}
