using NUlid;

namespace UniversityManagementSystem.Core.Entities
{
    public class Admin : BaseEntity
    {
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;

        // Foreign Key to SystemUser
        public Ulid SystemUserId { get; set; }

        // Navigation Property
        public SystemUser SystemUser { get; set; } = null!;
    }
}
