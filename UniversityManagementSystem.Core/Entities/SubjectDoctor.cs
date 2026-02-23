using System;

namespace UniversityManagementSystem.Core.Entities
{
    public class SubjectDoctor
    {
        public int SubjectId { get; set; }
        public int DoctorId { get; set; }

        // Navigation Properties
        public Subject Subject { get; set; } = null!;
        public Doctor Doctor { get; set; } = null!;
    }
}
