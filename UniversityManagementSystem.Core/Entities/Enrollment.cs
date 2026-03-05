using System;
using NUlid;

namespace UniversityManagementSystem.Core.Entities
{
    public class Enrollment : BaseEntity
    {
        public Ulid StudentId { get; set; }
        public Ulid SubjectOfferingId { get; set; }
        public DateTime EnrolledAt { get; set; } = DateTime.UtcNow;
        public bool IsActive { get; set; } = true;

        // Navigation Properties
        public Student Student { get; set; } = null!;
        public SubjectOffering SubjectOffering { get; set; } = null!;
    }
}
