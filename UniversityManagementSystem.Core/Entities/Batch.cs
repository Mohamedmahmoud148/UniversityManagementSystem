using System.Collections.Generic;
using NUlid;

namespace UniversityManagementSystem.Core.Entities
{
    public class Batch : BaseEntity
    {
        public string Name { get; set; } = string.Empty;
        public Ulid DepartmentId { get; set; }

        // Navigation Properties
        public Department Department { get; set; } = null!;
        public ICollection<Student> Students { get; set; } = [];
        public ICollection<Subject> Subjects { get; set; } = [];
        public ICollection<Group> Groups { get; set; } = [];
    }
}
