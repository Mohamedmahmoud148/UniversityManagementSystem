using System.Collections.Generic;

namespace UniversityManagementSystem.Core.Entities
{
    public class Student : BaseEntity
    {
        public string FullName { get; set; } = string.Empty;
        public string UniversityStudentId { get; set; } = string.Empty;
        
        private string _phone = string.Empty;
        public string Phone 
        { 
            get => _phone;
            set
            {
                if (!System.Text.RegularExpressions.Regex.IsMatch(value, @"^01[0125][0-9]{8}$"))
                    throw new Exceptions.DomainException("Invalid Egyptian phone number.");
                _phone = value;
            }
        }
        public string Email { get; set; } = string.Empty; // Personal Email
        
        public int UniversityId { get; set; } // Required
        public int CollegeId { get; set; }
        public int DepartmentId { get; set; }
        public int BatchId { get; set; }
        public int GroupId { get; set; } // Required
        public bool IsActive { get; set; } = true;

        // Foreign Key to SystemUser
        public int SystemUserId { get; set; }

        // Navigation Properties
        public SystemUser SystemUser { get; set; } = null!;
        public University University { get; set; } = null!;
        public College College { get; set; } = null!;
        public Department Department { get; set; } = null!;
        public Batch Batch { get; set; } = null!;
        public Group Group { get; set; } = null!;
        public ICollection<Enrollment> Enrollments { get; set; } = new List<Enrollment>();
    }
}
