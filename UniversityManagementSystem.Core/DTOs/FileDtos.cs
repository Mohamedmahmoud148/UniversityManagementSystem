using System;
using NUlid;

namespace UniversityManagementSystem.Core.DTOs
{
    public class UploadFileDto
    {
        public string FileName { get; set; } = string.Empty;
        public string Base64Content { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
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
