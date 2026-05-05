using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;
using UniversityManagementSystem.Core.Entities;
using NUlid;

namespace UniversityManagementSystem.Core.DTOs
{
    public class RegulationSubjectDto
    {
        public Ulid SubjectId { get; set; }
        public int Semester { get; set; }
        public bool IsRequired { get; set; }
    }
    public class RegulationDto
    {
        public Ulid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Content { get; set; }
        public string Type { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        /// <summary>The ID of the attached file, if any.</summary>
        public string? FileId { get; set; }
        /// <summary>60-minute signed download URL. Null if no file is attached.</summary>
        public string? FileUrl { get; set; }

        public string? DepartmentId { get; set; }
        public List<RegulationSubjectDto> Subjects { get; set; } = new();
    }

    /// <summary>
    /// Used with multipart/form-data.
    /// Send EITHER Content (text) OR File (PDF/Word/Excel/TXT). Both are optional,
    /// but at least one must be present.
    /// </summary>
    public class CreateRegulationDto
    {
        public string Title { get; set; } = string.Empty;

        /// <summary>Text content of the regulation (optional when File is provided).</summary>
        public string? Content { get; set; }

        /// <summary>Regulation type enum value (int).</summary>
        public RegulationType Type { get; set; }

        /// <summary>
        /// File attachment (PDF, Word, Excel, TXT).
        /// Bound from multipart/form-data — ignored in JSON serialization.
        /// </summary>
        [JsonIgnore]
        public IFormFile? File { get; set; }

        public string? DepartmentId { get; set; }

        /// <summary>
        /// JSON string representing a list of RegulationSubjectDto.
        /// Required because this is a multipart/form-data request.
        /// </summary>
        public string? SubjectsJson { get; set; }
    }

    /// <summary>
    /// Used with multipart/form-data for PUT /api/Regulations/{id}.
    /// </summary>
    public class UpdateRegulationDto
    {
        public string Title { get; set; } = string.Empty;
        public string? Content { get; set; }
        public RegulationType Type { get; set; }
        public bool IsActive { get; set; }

        /// <summary>
        /// Optionally replace the attached file.
        /// Bound from multipart/form-data.
        /// </summary>
        [JsonIgnore]
        public IFormFile? File { get; set; }

        public string? DepartmentId { get; set; }
        public string? SubjectsJson { get; set; }
    }
}
