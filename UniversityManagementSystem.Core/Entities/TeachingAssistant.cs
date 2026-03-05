using System.Collections.Generic;
using NUlid;

namespace UniversityManagementSystem.Core.Entities
{
    public class TeachingAssistant : BaseEntity
    {
        public string FullName { get; set; } = string.Empty;
        public string UniversityStaffId { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string UniversityEmail { get; set; } = string.Empty;
        public Ulid DepartmentId { get; set; }
        public Ulid SystemUserId { get; set; }

        // Navigation Properties
        public SystemUser SystemUser { get; set; } = null!;
        public Department Department { get; set; } = null!;
        public ICollection<SubjectAssistant> SubjectAssistants { get; set; } = [];
    }
}
