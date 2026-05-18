using System;
using NUlid;

namespace UniversityManagementSystem.Core.Entities
{
    /// <summary>
    /// Queue entry added when a student tries to enroll in a full offering.
    /// When a spot opens (unenrollment), the student at Position=1 gets notified.
    /// </summary>
    public class SubjectOfferingWaitlist : BaseEntity
    {
        public Ulid StudentId         { get; set; }
        public Ulid SubjectOfferingId { get; set; }

        /// <summary>1-based queue position. Lower = higher priority.</summary>
        public int Position  { get; set; }
        public DateTime AddedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public Student         Student  { get; set; } = null!;
        public SubjectOffering Offering { get; set; } = null!;
    }
}
