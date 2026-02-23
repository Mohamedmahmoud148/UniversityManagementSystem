using System;

namespace UniversityManagementSystem.Core.Entities
{
    public class UploadedFile : BaseEntity
    {
        public string FileName { get; set; } = string.Empty;
        public string StoredPath { get; set; } = string.Empty; // Path in storage
        public string ContentType { get; set; } = string.Empty; // pdf, image, excel
        public long FileSizeBytes { get; set; }

        public int UploadedByUserId { get; set; }
        public int? SubjectId { get; set; } // Context link

        // Academic Material Hardening - Fixed: Nullable to allow existing data
        public int? SubjectOfferingId { get; set; } 
        public int? UploadedByDoctorId { get; set; } 
        public DateTime UploadDate { get; set; } = DateTime.UtcNow;

        public string ValidationStatus { get; set; } = "Pending"; // Pending, Validated, Rejected
        public string? ExtractedDataJson { get; set; } // JSON extracted by AI
        public string? ValidationErrors { get; set; }

        // Navigation Properties
        public Subject? Subject { get; set; }
        public SystemUser UploadedBy { get; set; } = null!;
        public SubjectOffering? SubjectOffering { get; set; }
        public Doctor? UploadedByDoctor { get; set; }
    }
}
