using System;
using NUlid;

namespace UniversityManagementSystem.Core.Entities
{
    public class RegulationSubject : BaseEntity
    {
        public Ulid RegulationId { get; set; }
        public Regulation Regulation { get; set; } = null!;

        public Ulid SubjectId { get; set; }
        public Subject Subject { get; set; } = null!;

        public int Semester { get; set; }  // Semester the subject is taught (e.g., 1-8)
        public bool IsRequired { get; set; } = true;  // Mandatory or Optional
    }
}
