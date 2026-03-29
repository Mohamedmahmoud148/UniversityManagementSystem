using System;
using Microsoft.AspNetCore.Http;
using NUlid;

namespace UniversityManagementSystem.Core.DTOs
{
    /// <summary>
    /// Response returned after a successful file upload to R2.
    /// </summary>
    public class FileUploadResponseDto
    {
        public string FileId { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public long Size { get; set; }
        /// <summary>
        /// The signed URL (60-min expiry) for the uploaded file.
        /// </summary>
        public string Url { get; set; } = string.Empty;
    }

    public class FileStatusDto
    {
        public Ulid Id { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string? ExtractedData { get; set; }
        public string? Errors { get; set; }
    }

    public class RenameFileDto
    {
        public string NewFileName { get; set; } = string.Empty;
    }
}
