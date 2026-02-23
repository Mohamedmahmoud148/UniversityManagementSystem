using System;

namespace UniversityManagementSystem.Core.Entities
{
    public class Enrollment : BaseEntity
    {
        public int StudentId { get; set; }
        public int SubjectOfferingId { get; set; }
        public DateTime EnrolledAt { get; set; } = DateTime.UtcNow;
        public bool IsActive { get; set; } = true;

        // Navigation Properties
        public Student Student { get; set; } = null!;
        public SubjectOffering SubjectOffering { get; set; } = null!;
    }
}
