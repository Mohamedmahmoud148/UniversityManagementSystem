using System;
using NUlid;

namespace UniversityManagementSystem.Core.Entities
{
    public class StudentFile : BaseEntity
    {
        public string FileName { get; set; } = string.Empty;

        /// <summary>
        /// The R2 object key e.g. "student-files/guid_file.pdf".
        /// Use IStorageService.GenerateSignedUrlAsync(StorageKey) for downloads.
        /// </summary>
        public string StorageKey { get; set; } = string.Empty;

        public string ContentType { get; set; } = string.Empty;
        public long FileSizeBytes { get; set; }

        /// <summary>FK → Student who uploaded this file.</summary>
        public Ulid UploadedByStudentId { get; set; }

        /// <summary>
        /// FK → UploadedFiles.Id
        /// </summary>
        public Ulid? FileId { get; set; }

        /// <summary>
        /// Text extracted from the file (for AI queries).
        /// Populated for PDF and TXT files. Null for other types.
        /// </summary>
        public string? ExtractedText { get; set; }

        // Navigation
        public Student UploadedByStudent { get; set; } = null!;
        public UploadedFile? File { get; set; }
    }
}
