using System.ComponentModel.DataAnnotations;

namespace UniversityManagementSystem.Core.DTOs
{
    public class SubjectOfferingDto
    {
        public int Id { get; set; }
        
        public int SubjectId { get; set; }
        public string SubjectName { get; set; } = string.Empty;

        public int SemesterId { get; set; }
        public string SemesterName { get; set; } = string.Empty;

        public int DoctorId { get; set; }
        public string DoctorName { get; set; } = string.Empty;

        public int MaxCapacity { get; set; }
        public int DepartmentId { get; set; }
        public int BatchId { get; set; }
        public int? GroupId { get; set; }
    }

    public class CreateSubjectOfferingDto
    {
        [Required]
        public int SubjectId { get; set; }

        [Required]
        public int SemesterId { get; set; }

        [Required]
        public int DoctorId { get; set; }

        [Required]
        public int DepartmentId { get; set; }

        [Required]
        public int BatchId { get; set; }

        public int? GroupId { get; set; }

        [Range(1, 1000)]
        public int MaxCapacity { get; set; }
    }
}
