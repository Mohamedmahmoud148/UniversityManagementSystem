using NUlid;

namespace UniversityManagementSystem.Core.Entities
{
    // Join table — no BaseEntity inheritance (composite PK handled by EF config)
    public class SubjectDoctor
    {
        public Ulid SubjectId { get; set; }
        public Ulid DoctorId { get; set; }

        // Navigation Properties
        public Subject Subject { get; set; } = null!;
        public Doctor Doctor { get; set; } = null!;
    }
}
