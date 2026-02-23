using System.Collections.Generic;

namespace UniversityManagementSystem.Core.Entities
{
    public class University : BaseEntity
    {
        public string Name { get; set; } = string.Empty;

        // Navigation Properties
        public ICollection<College> Colleges { get; set; } = [];
    }
}
