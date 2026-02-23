using System.Collections.Generic;

namespace UniversityManagementSystem.Core.Entities
{
    public class College : BaseEntity
    {
        public string Name { get; set; } = string.Empty;
        public int UniversityId { get; set; }

        // Navigation Properties
        public University University { get; set; } = null!;
        public ICollection<Department> Departments { get; set; } = new List<Department>();
        public ICollection<Subject> Subjects { get; set; } = new List<Subject>();
    }
}
