using System.Collections.Generic;
using NUlid;

namespace UniversityManagementSystem.Core.Entities
{
    public class Subject : BaseEntity
    {
        public string Name { get; set; } = string.Empty;
        /// <summary>Short academic code, e.g. "CS101". Used as the human-readable Code field.</summary>
        public Ulid? CollegeId { get; set; }
        public Ulid DepartmentId { get; set; }
        public Ulid? BatchId { get; set; }
        public int CreditHours { get; set; }

        // Navigation Properties
        public College? College { get; set; }
        public Department Department { get; set; } = null!;
        public Batch? Batch { get; set; }

        public ICollection<SubjectDoctor> SubjectDoctors { get; set; } = new List<SubjectDoctor>();
        public ICollection<SubjectAssistant> SubjectAssistants { get; set; } = new List<SubjectAssistant>();
        public ICollection<Enrollment> Enrollments { get; set; } = new List<Enrollment>();
    }
}
