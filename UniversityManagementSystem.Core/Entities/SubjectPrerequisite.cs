using NUlid;

namespace UniversityManagementSystem.Core.Entities
{
    /// <summary>
    /// Defines that SubjectId requires PrerequisiteSubjectId to be completed first.
    /// Used by the RegistrationService to block ineligible enrollments.
    /// </summary>
    public class SubjectPrerequisite : BaseEntity
    {
        public Ulid SubjectId             { get; set; }
        public Ulid PrerequisiteSubjectId { get; set; }

        /// <summary>
        /// Optional minimum passing score (0-100). Null means any passing grade (D or above).
        /// </summary>
        public double? MinimumGrade { get; set; }

        // Navigation
        public Subject Subject             { get; set; } = null!;
        public Subject PrerequisiteSubject { get; set; } = null!;
    }
}
