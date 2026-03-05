using System;
using NUlid;

namespace UniversityManagementSystem.Core.DTOs
{
    public class MaterialDto
    {
        public Ulid Id { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public DateTime UploadedAt { get; set; }
    }

    /// <summary>Lightweight metadata response for GET /api/materials/{id}/metadata.</summary>
    public record MaterialMetadataDto(
        string MaterialId,
        string FileName,
        string FileUrl,
        string SubjectOfferingId
    );
}

