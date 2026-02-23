using System;

namespace UniversityManagementSystem.Core.Entities
{
    public class Material : BaseEntity
    {
        public string FileName { get; set; } = string.Empty; // Original name
        public string StoredFileName { get; set; } = string.Empty; // GUID-based name
        public string ContentType { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

        public int SubjectOfferingId { get; set; }
        public int UploadedByDoctorId { get; set; }

        // Navigation Properties
        public SubjectOffering SubjectOffering { get; set; } = null!;
        public Doctor UploadedByDoctor { get; set; } = null!;
    }
}
