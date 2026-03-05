using NUlid;

namespace UniversityManagementSystem.Core.Entities
{
    // Join table — no BaseEntity inheritance (composite PK handled by EF config)
    public class SubjectAssistant
    {
        public Ulid SubjectId { get; set; }
        public Ulid TeachingAssistantId { get; set; }

        // Navigation Properties
        public Subject Subject { get; set; } = null!;
        public TeachingAssistant TeachingAssistant { get; set; } = null!;
    }
}
