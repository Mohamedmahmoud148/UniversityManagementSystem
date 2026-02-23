using System.Collections.Generic;

namespace UniversityManagementSystem.Core.Entities
{
    public class Doctor : BaseEntity
    {
        public string FullName { get; set; } = string.Empty;
        public string UniversityStaffId { get; set; } = string.Empty;

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
        public string Email { get; set; } = string.Empty;
        public int DepartmentId { get; set; }

        // Foreign Key to SystemUser
        public int SystemUserId { get; set; }

        // Navigation Properties
        public SystemUser SystemUser { get; set; } = null!;
        public Department Department { get; set; } = null!;
        public ICollection<SubjectDoctor> SubjectDoctors { get; set; } = new List<SubjectDoctor>();
    }
}
