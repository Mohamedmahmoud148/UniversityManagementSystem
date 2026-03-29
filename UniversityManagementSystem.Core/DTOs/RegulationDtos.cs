using UniversityManagementSystem.Core.Entities;
using NUlid;

namespace UniversityManagementSystem.Core.DTOs
{
    public class RegulationDto
    {
        public Ulid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Content { get; set; }
        public string Type { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        /// <summary>The associated file ID, if this regulation has an attached file.</summary>
        public string? FileId { get; set; }
        /// <summary>A 60-minute signed URL for the attached file. Null if no file attached.</summary>
        public string? FileUrl { get; set; }
    }

    public class CreateRegulationDto
    {
        public string Title { get; set; } = string.Empty;

        /// <summary>The text body of the regulation. Either Content or FileId must be provided.</summary>
        public string? Content { get; set; }

        /// <summary>
        /// The ULID of a previously uploaded file (via POST /api/File/upload).
        /// Either Content or FileId must be provided.
        /// </summary>
        public string? FileId { get; set; }

        public RegulationType Type { get; set; }
    }

    public class UpdateRegulationDto
    {
        public string Title { get; set; } = string.Empty;
        public string? Content { get; set; }
        public string? FileId { get; set; }
        public RegulationType Type { get; set; }
        public bool IsActive { get; set; }
    }
}
