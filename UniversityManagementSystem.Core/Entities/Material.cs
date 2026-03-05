using System;
using NUlid;

namespace UniversityManagementSystem.Core.Entities
{
    public class Material : BaseEntity
    {
        public string FileName { get; set; } = string.Empty;
        public string StoredFileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

        public Ulid SubjectOfferingId { get; set; }
        public Ulid UploadedByDoctorId { get; set; }

        // Navigation Properties
        public SubjectOffering SubjectOffering { get; set; } = null!;
        public Doctor UploadedByDoctor { get; set; } = null!;
    }
}
