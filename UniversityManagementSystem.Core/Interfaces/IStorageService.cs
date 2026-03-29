using System.IO;
using System.Threading.Tasks;

namespace UniversityManagementSystem.Core.Interfaces
{
    /// <summary>
    /// Abstraction over object storage (Cloudflare R2 / S3-compatible).
    /// </summary>
    public interface IStorageService
    {
        /// <summary>
        /// Uploads a stream to the specified folder and returns the full public URL.
        /// </summary>
        Task<string> UploadAsync(Stream stream, string fileName, string contentType, string folder);

        /// <summary>
        /// Deletes an object by its storage key (the path after the bucket root, e.g. "materials/abc.pdf").
        /// </summary>
        Task DeleteAsync(string objectKey);
    }
}
