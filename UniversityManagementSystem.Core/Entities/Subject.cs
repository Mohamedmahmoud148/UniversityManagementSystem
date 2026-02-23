using System.Collections.Generic;

namespace UniversityManagementSystem.Core.Entities
{
    public class Subject : BaseEntity
    {
        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public int? CollegeId { get; set; }
        public int DepartmentId { get; set; }
        public int? BatchId { get; set; }
        public int CreditHours { get; set; } // Added

        // Navigation Properties
        public College? College { get; set; }
        public Department Department { get; set; } = null!;
        public Batch? Batch { get; set; }

        public ICollection<SubjectDoctor> SubjectDoctors { get; set; } = new List<SubjectDoctor>();
        public ICollection<SubjectAssistant> SubjectAssistants { get; set; } = new List<SubjectAssistant>();
        public ICollection<Enrollment> Enrollments { get; set; } = new List<Enrollment>();
    }
}
