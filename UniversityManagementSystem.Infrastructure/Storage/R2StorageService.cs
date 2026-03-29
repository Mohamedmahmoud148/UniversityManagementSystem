using System;
using System.IO;
using System.Threading.Tasks;
using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;
using UniversityManagementSystem.Core.Interfaces;

namespace UniversityManagementSystem.Infrastructure.Storage
{
    public class R2StorageService : IStorageService, IDisposable
    {
        private readonly AmazonS3Client _s3;
        private readonly R2Settings _settings;

        public R2StorageService(IOptions<R2Settings> options)
        {
            _settings = options.Value;

            var credentials = new BasicAWSCredentials(_settings.AccessKey, _settings.SecretKey);
            var config = new AmazonS3Config
            {
                ServiceURL = _settings.ServiceUrl,
                ForcePathStyle = true,       // Required for R2 / non-AWS S3 endpoints
                AuthenticationRegion = "auto" // Cloudflare R2 uses "auto"
            };

            _s3 = new AmazonS3Client(credentials, config);
        }

        /// <inheritdoc/>
        public async Task<string> UploadAsync(Stream stream, string fileName, string contentType, string folder)
        {
            // Build a unique object key: folder/guid_filename
            var sanitized = SanitizeFileName(fileName);
            var key = $"{folder.TrimEnd('/')}/{Guid.NewGuid()}_{sanitized}";

            var request = new PutObjectRequest
            {
                BucketName = _settings.BucketName,
                Key = key,
                InputStream = stream,
                ContentType = contentType,
                // Do NOT set CannedACL — Cloudflare R2 ignores ACLs and errors if set
            };

            await _s3.PutObjectAsync(request);

            // Return the full public URL
            var baseUrl = _settings.PublicBaseUrl.TrimEnd('/');
            return $"{baseUrl}/{key}";
        }

        /// <inheritdoc/>
        public async Task DeleteAsync(string objectKey)
        {
            if (string.IsNullOrWhiteSpace(objectKey)) return;

            var request = new DeleteObjectRequest
            {
                BucketName = _settings.BucketName,
                Key = objectKey
            };

            await _s3.DeleteObjectAsync(request);
        }

        /// <summary>
        /// Extracts the R2 object key from a full public URL stored in the database.
        /// e.g. "https://pub-xxx.r2.dev/materials/abc.pdf" → "materials/abc.pdf"
        /// </summary>
        public static string ExtractKeyFromUrl(string publicBaseUrl, string fullUrl)
        {
            var prefix = publicBaseUrl.TrimEnd('/') + "/";
            return fullUrl.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                ? fullUrl[prefix.Length..]
                : fullUrl; // Fallback: treat as raw key
        }

        private static string SanitizeFileName(string fileName)
        {
            // Replace spaces and dangerous characters
            return Path.GetFileName(fileName)
                .Replace(" ", "_")
                .Replace("#", "")
                .Replace("?", "")
                .Replace("&", "");
        }

        public void Dispose() => _s3?.Dispose();
    }
}
