using System;
using NUlid;

namespace UniversityManagementSystem.Core.Entities
{
    public enum EnrollmentUploadStatus
    {
        Processing,
        Completed,
        Failed
    }

    public class EnrollmentUpload : BaseEntity
    {
        public string FileName { get; set; } = string.Empty;

        /// <summary>
        /// The R2 object key e.g. "enrollment/guid_file.xlsx".
        /// </summary>
        public string StorageKey { get; set; } = string.Empty;

        /// <summary>FK → SystemUser (Admin) who uploaded this file.</summary>
        public Ulid UploadedByAdminId { get; set; }

        public EnrollmentUploadStatus Status { get; set; } = EnrollmentUploadStatus.Processing;

        /// <summary>Number of student records successfully created.</summary>
        public int CreatedCount { get; set; }

        /// <summary>Number of rows skipped because students already existed.</summary>
        public int SkippedCount { get; set; }

        /// <summary>Comma-separated error messages, if any rows failed.</summary>
        public string? Errors { get; set; }

        // Navigation
        public SystemUser UploadedByAdmin { get; set; } = null!;
    }
}
