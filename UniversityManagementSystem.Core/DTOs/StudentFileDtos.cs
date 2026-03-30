using System;
using NUlid;

namespace UniversityManagementSystem.Core.DTOs
{
    /// <summary>
    /// Returned immediately after a student uploads a file.
    /// </summary>
    public class StudentFileUploadResponseDto
    {
        public string FileId { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public long Size { get; set; }
        /// <summary>True if text was successfully extracted (PDF/TXT).</summary>
        public bool TextExtracted { get; set; }
    }

    /// <summary>
    /// Returned when listing a student's files.
    /// </summary>
    public class StudentFileDto
    {
        public string FileId { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public long Size { get; set; }
        public bool HasExtractedText { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
