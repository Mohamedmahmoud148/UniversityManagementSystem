using System.Collections.Generic;
using NUlid;

namespace UniversityManagementSystem.Core.Entities
{
    public class Group : BaseEntity
    {
        public string Name { get; set; } = string.Empty; // e.g., "G1", "Group A"
        public Ulid BatchId { get; set; }

        // Navigation Properties
        public Batch Batch { get; set; } = null!;
        public ICollection<Student> Students { get; set; } = new List<Student>();
        public ICollection<SubjectOffering> SubjectOfferings { get; set; } = new List<SubjectOffering>();
    }
}
