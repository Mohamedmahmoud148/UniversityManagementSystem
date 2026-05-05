using System;
using NUlid;

namespace UniversityManagementSystem.Core.Entities
{
    public class Regulation : BaseEntity
    {
        public string Title { get; set; } = string.Empty;

        /// <summary>Text content of the regulation. Nullable — a regulation may be file-only.</summary>
        public string? Content { get; set; }

        public RegulationType Type { get; set; }
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Optional FK to an UploadedFile record. Set when the regulation has an
        /// attached PDF or document instead of (or in addition to) text content.
        /// </summary>
        public Ulid? FileId { get; set; }

        // Navigation property
        public UploadedFile? File { get; set; }

        public Ulid? DepartmentId { get; set; }
        public Department? Department { get; set; }

        public ICollection<RegulationSubject> RegulationSubjects { get; set; } = new List<RegulationSubject>();
    }

    public enum RegulationType
    {
        Academic,
        Conduct,
        Exam,
        General
    }
}
