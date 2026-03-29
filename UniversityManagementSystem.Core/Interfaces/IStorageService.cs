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
        /// Uploads a stream to the specified folder and returns the <b>storage object key</b>
        /// (e.g. "materials/guid_file.pdf") — NOT the full URL.
        /// Use <see cref="BuildUrl"/> to construct the public URL, or
        /// <see cref="GenerateSignedUrlAsync"/> for secure downloads.
        /// </summary>
        Task<string> UploadAsync(Stream stream, string fileName, string contentType, string folder);

        /// <summary>
        /// Deletes an object by its storage key (e.g. "materials/guid_file.pdf").
        /// </summary>
        Task DeleteAsync(string objectKey);

        /// <summary>
        /// Generates a pre-signed URL for the given object key that expires after
        /// <paramref name="expiryMinutes"/> minutes. Use for all downloads — never expose the raw public URL.
        /// </summary>
        Task<string> GenerateSignedUrlAsync(string objectKey, int expiryMinutes = 60);

        /// <summary>
        /// Builds the full public CDN URL from the object key.
        /// Use only for non-sensitive, publicly accessible resources.
        /// </summary>
        string BuildUrl(string objectKey);
    }
}
