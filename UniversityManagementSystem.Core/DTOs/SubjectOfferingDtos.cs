using System.ComponentModel.DataAnnotations;
using NUlid;

namespace UniversityManagementSystem.Core.DTOs
{
    public class SubjectOfferingDto
    {
        public Ulid Id { get; set; }

        public Ulid SubjectId { get; set; }
        public string SubjectCode { get; set; } = string.Empty;
        public string SubjectName { get; set; } = string.Empty;

        public Ulid SemesterId { get; set; }
        public string SemesterName { get; set; } = string.Empty;

        public Ulid DoctorId { get; set; }
        public string DoctorName { get; set; } = string.Empty;

        public int MaxCapacity { get; set; }
        public Ulid DepartmentId { get; set; }
        public string DepartmentName { get; set; } = string.Empty;
        
        public Ulid BatchId { get; set; }
        public string BatchName { get; set; } = string.Empty;
        
        public Ulid? GroupId { get; set; }
    }

    public class CreateSubjectOfferingDto
    {
        [Required]
        public string SubjectCode { get; set; } = string.Empty;

        [Required]
        public string SemesterId { get; set; } = string.Empty;

        [Required]
        public string DoctorCode { get; set; } = string.Empty;

        [Required]
        public string DepartmentCode { get; set; } = string.Empty;

        [Required]
        public string BatchCode { get; set; } = string.Empty;

        public string? GroupCode { get; set; }

        [Range(1, 1000)]
        public int MaxCapacity { get; set; }
    }
}
