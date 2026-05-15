using System;
using NUlid;

namespace UniversityManagementSystem.Core.Entities
{
    public class Material : BaseEntity
    {
        public string FileName { get; set; } = string.Empty;

        /// <summary>Human-readable display title set by the uploader.</summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>Optional description / notes about this material.</summary>
        public string? Description { get; set; }

        /// <summary>
        /// The R2 object key (e.g. "materials/guid_file.pdf").
        /// Use IStorageService.BuildUrl(StorageKey) for public URL,
        /// or GenerateSignedUrlAsync(StorageKey) for secure downloads.
        /// </summary>
        public string StorageKey { get; set; } = string.Empty;

        /// <summary>Legacy field — kept for backward compatibility. Mirrors StorageKey.</summary>
        public string StoredFileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

        public Ulid SubjectOfferingId { get; set; }
        public Ulid UploadedByDoctorId { get; set; }
        
        /// <summary>
        /// FK → UploadedFiles.Id
        /// </summary>
        public Ulid? FileId { get; set; }

        // Navigation Properties
        public SubjectOffering SubjectOffering { get; set; } = null!;
        public Doctor UploadedByDoctor { get; set; } = null!;
        public UploadedFile? File { get; set; }
    }
}
