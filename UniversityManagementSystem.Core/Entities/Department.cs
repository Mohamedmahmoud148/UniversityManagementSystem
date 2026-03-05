using System.Collections.Generic;
using NUlid;

namespace UniversityManagementSystem.Core.Entities
{
    public class Department : BaseEntity
    {
        public string Name { get; set; } = string.Empty;
        public Ulid CollegeId { get; set; }

        // Navigation Properties
        public College College { get; set; } = null!;
        public ICollection<Batch> Batches { get; set; } = [];
        public ICollection<Doctor> Doctors { get; set; } = [];
        public ICollection<TeachingAssistant> TeachingAssistants { get; set; } = [];
        public ICollection<Subject> Subjects { get; set; } = [];
    }
}
