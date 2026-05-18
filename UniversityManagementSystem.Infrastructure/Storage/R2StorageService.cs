using System;
using System.IO;
using System.Threading.Tasks;
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

            // ── Read credentials from environment variables first (production-safe) ──
            // Set: R2__AccessKey and R2__SecretKey as environment variables.
            // .NET configuration automatically maps R2__AccessKey → R2:AccessKey,
            // so these are already bound via IOptions<R2Settings>.
            // R2Settings.AccessKey/SecretKey are populated from env vars at startup.
            // Force SigV4 globally for all S3 presigned URL generation (R2 rejects SigV2)
            Amazon.AWSConfigsS3.UseSignatureVersion4 = true;

            var credentials = new BasicAWSCredentials(_settings.AccessKey, _settings.SecretKey);
            var config = new AmazonS3Config
            {
                ServiceURL = _settings.ServiceUrl,
                ForcePathStyle = true,         // Required for R2 / non-AWS S3 endpoints
                AuthenticationRegion = "auto"  // R2 requires "auto" for SigV4 presigned URLs
            };

            _s3 = new AmazonS3Client(credentials, config);
        }

        /// <inheritdoc/>
        /// <returns>
        /// The <b>storage object key</b> (e.g. "materials/guid_file.pdf"),
        /// NOT the full URL. Use <see cref="BuildUrl"/> or <see cref="GenerateSignedUrlAsync"/>.
        /// </returns>
        public async Task<string> UploadAsync(Stream stream, string fileName, string contentType, string folder)
        {
            var sanitized = SanitizeFileName(fileName);
            var key = $"{folder.TrimEnd('/')}/{Guid.NewGuid()}_{sanitized}";

            // ── Buffer into MemoryStream ────────────────────────────────────────
            // IFormFile.OpenReadStream() returns a non-seekable HttpRequestStream.
            // The AWS SDK must know the exact Content-Length before signing;
            // a non-seekable stream causes a signature mismatch on Cloudflare R2.
            // Copying to MemoryStream guarantees seekability and a correct Length.
            using var memStream = new MemoryStream();
            await stream.CopyToAsync(memStream);
            memStream.Position = 0;

            var request = new PutObjectRequest
            {
                BucketName = _settings.BucketName,
                Key = key,
                InputStream = memStream,
                ContentType = contentType,
                AutoCloseStream = false,        // we own the using block
                UseChunkEncoding = false,        // R2 does not support chunked streaming
                DisablePayloadSigning = true
                // Do NOT set CannedACL — Cloudflare R2 ignores ACLs and may error
            };

            // Set explicit Content-Length — prevents SDK from using Transfer-Encoding: chunked
            request.Headers.ContentLength = memStream.Length;

            await _s3.PutObjectAsync(request);

            return key;
        }


        /// <inheritdoc/>
        public async Task DeleteAsync(string objectKey)
        {
            if (string.IsNullOrWhiteSpace(objectKey)) return;

            // Accept either a raw key or a full URL — extract key if needed
            if (objectKey.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                objectKey = ExtractKeyFromUrl(objectKey);

            var request = new DeleteObjectRequest
            {
                BucketName = _settings.BucketName,
                Key = objectKey
            };

            await _s3.DeleteObjectAsync(request);
        }

        /// <inheritdoc/>
        public Task<string> GenerateSignedUrlAsync(string objectKey, int expiryMinutes = 60)
        {
            // Accept either a raw key or a full URL — extract key if needed
            if (objectKey.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                objectKey = ExtractKeyFromUrl(objectKey);

            var request = new GetPreSignedUrlRequest
            {
                BucketName = _settings.BucketName,
                Key = objectKey,
                Verb = HttpVerb.GET,
                Expires = DateTime.UtcNow.AddMinutes(expiryMinutes),
                Protocol = Protocol.HTTPS
            };

            return Task.FromResult(_s3.GetPreSignedURL(request));
        }

        /// <inheritdoc/>
        public string BuildUrl(string objectKey)
        {
            var baseUrl = _settings.PublicBaseUrl;
            
            // Safeguard: If the environment variable on Railway is accidentally set to the ServiceUrl 
            // instead of the PublicBaseUrl, force it to use the public dev URL.
            if (string.IsNullOrWhiteSpace(baseUrl) || baseUrl.Contains("cloudflarestorage.com"))
            {
                baseUrl = "https://pub-f6f1bfde1fb94edc9d516cab5cf086f1.r2.dev";
            }

            // Construct the public CDN URL
            return $"{baseUrl.TrimEnd('/')}/{objectKey}";
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        /// <summary>
        /// Extracts the R2 object key from a full URL stored in the database.
        /// Handles both the new CDN domain and old S3-endpoint URLs for backward compatibility.
        /// e.g. "https://files.yourdomain.com/materials/abc.pdf" → "materials/abc.pdf"
        /// </summary>
        private string ExtractKeyFromUrl(string fullUrl)
        {
            // Try PublicBaseUrl first
            var prefix = _settings.PublicBaseUrl.TrimEnd('/') + "/";
            if (fullUrl.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return fullUrl[prefix.Length..];

            // Fallback: strip the S3 service URL + bucket name (old format)
            var oldPrefix = $"{_settings.ServiceUrl.TrimEnd('/')}/{_settings.BucketName}/";
            if (fullUrl.StartsWith(oldPrefix, StringComparison.OrdinalIgnoreCase))
                return fullUrl[oldPrefix.Length..];

            // Last resort: treat as raw key
            return fullUrl;
        }

        private static string SanitizeFileName(string fileName) =>
            System.IO.Path.GetFileName(fileName)
                .Replace(" ", "_")
                .Replace("#", "")
                .Replace("?", "")
                .Replace("&", "");

        /// <inheritdoc/>
        public async Task<Stream> DownloadAsync(string objectKey)
        {
            if (objectKey.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                objectKey = ExtractKeyFromUrl(objectKey);

            var request = new GetObjectRequest
            {
                BucketName = _settings.BucketName,
                Key = objectKey
            };

            var response = await _s3.GetObjectAsync(request);
            var ms = new MemoryStream();
            await response.ResponseStream.CopyToAsync(ms);
            ms.Position = 0;
            return ms;
        }

        public void Dispose() => _s3?.Dispose();
    }
}
