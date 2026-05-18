using System;
using System.Collections.Generic;
using NUlid;

namespace UniversityManagementSystem.Core.Entities
{
    public enum StudentType
    {
        Regular    = 0,  // منتظم
        Transfer   = 1,  // منقول
        Repeating  = 2,  // معيد
        External   = 3   // انتساب
    }

    public enum Gender
    {
        Male   = 0,
        Female = 1
    }

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
                if (!string.IsNullOrEmpty(value) &&
                    !System.Text.RegularExpressions.Regex.IsMatch(value, @"^01[0125][0-9]{8}$"))
                    throw new Exceptions.DomainException("Invalid Egyptian phone number.");
                _phone = value;
            }
        }

        public string Email { get; set; } = string.Empty;       // Personal email (optional)
        public string NationalId { get; set; } = string.Empty;  // Duplicated from SystemUser for quick lookup
        public string Governorate { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public Gender? Gender { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public StudentType StudentType { get; set; } = StudentType.Regular;
        public string Religion { get; set; } = string.Empty;

        public Ulid UniversityId { get; set; }
        public Ulid CollegeId { get; set; }
        public Ulid DepartmentId { get; set; }
        public Ulid BatchId { get; set; }
        public Ulid GroupId { get; set; }
        public bool IsActive { get; set; } = true;

        public Ulid SystemUserId { get; set; }
        public Ulid? RegulationId { get; set; }

        // Navigation Properties
        public Regulation? Regulation { get; set; }
        public SystemUser SystemUser { get; set; } = null!;
        public University University { get; set; } = null!;
        public College College { get; set; } = null!;
        public Department Department { get; set; } = null!;
        public Batch Batch { get; set; } = null!;
        public Group Group { get; set; } = null!;
        public ICollection<Enrollment> Enrollments { get; set; } = new List<Enrollment>();
    }
}
