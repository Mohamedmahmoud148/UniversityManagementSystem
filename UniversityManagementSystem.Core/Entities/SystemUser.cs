using System;

namespace UniversityManagementSystem.Core.Entities
{
    public class SystemUser : BaseEntity
    {
        private string _fullName = string.Empty;
        public string FullName
        {
            get => _fullName;
            set
            {
                if (!System.Text.RegularExpressions.Regex.IsMatch(value, @"^[\u0600-\u06FFa-zA-Z\s]{3,100}$"))
                    throw new Exceptions.DomainException("FullName must be 3-100 characters (Arabic/English).");
                _fullName = value;
            }
        }

        public string Email { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public UserRole Role { get; set; }

        // No loose RelatedEntityId. Relationships are defined in child entities (Student, Doctor, Admin).

        public string UniversityEmail { get; set; } = string.Empty;
        public string NationalId { get; set; } = string.Empty;

        public int? CreatedByUserId { get; set; } // ID of the user who created this account
        public bool IsActive { get; set; } = true;

        // Lockout Logic
        public int AccessFailedCount { get; set; }
        public DateTime? LockoutEnd { get; set; }
    }
}
