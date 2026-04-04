using System;
using NUlid;

namespace UniversityManagementSystem.Core.Entities
{
    public class UploadedFile : BaseEntity
    {
        public string FileName { get; set; } = string.Empty;

        /// <summary>
        /// The R2 object key (e.g. "files/guid_file.xlsx").
        /// Use IStorageService.BuildUrl(StorageKey) for public URL,
        /// or GenerateSignedUrlAsync(StorageKey) for secure downloads.
        /// </summary>
        public string StorageKey { get; set; } = string.Empty;

        public string ContentType { get; set; } = string.Empty;
        public long FileSizeBytes { get; set; }

        public Ulid UploadedByUserId { get; set; }
        public Ulid? SubjectId { get; set; }
        public Ulid? SubjectOfferingId { get; set; }
        public Ulid? UploadedByDoctorId { get; set; }
        public DateTime UploadDate { get; set; } = DateTime.UtcNow;

        public string ValidationStatus { get; set; } = "Pending";
        public string? ExtractedDataJson { get; set; }
        public string? ValidationErrors { get; set; }

        // Navigation Properties
        public Subject? Subject { get; set; }
        public SystemUser UploadedBy { get; set; } = null!;
        public SubjectOffering? SubjectOffering { get; set; }
        public Doctor? UploadedByDoctor { get; set; }
    }
}
