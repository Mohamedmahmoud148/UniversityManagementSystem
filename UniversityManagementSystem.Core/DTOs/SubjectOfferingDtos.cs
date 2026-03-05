using System.ComponentModel.DataAnnotations;
using NUlid;

namespace UniversityManagementSystem.Core.DTOs
{
    public class SubjectOfferingDto
    {
        public Ulid Id { get; set; }

        public Ulid SubjectId { get; set; }
        public string SubjectName { get; set; } = string.Empty;

        public Ulid SemesterId { get; set; }
        public string SemesterName { get; set; } = string.Empty;

        public Ulid DoctorId { get; set; }
        public string DoctorName { get; set; } = string.Empty;

        public int MaxCapacity { get; set; }
        public Ulid DepartmentId { get; set; }
        public Ulid BatchId { get; set; }
        public Ulid? GroupId { get; set; }
    }

    public class CreateSubjectOfferingDto
    {
        [Required]
        public Ulid SubjectId { get; set; }

        [Required]
        public Ulid SemesterId { get; set; }

        [Required]
        public Ulid DoctorId { get; set; }

        [Required]
        public Ulid DepartmentId { get; set; }

        [Required]
        public Ulid BatchId { get; set; }

        public Ulid? GroupId { get; set; }

        [Range(1, 1000)]
        public int MaxCapacity { get; set; }
    }
}
