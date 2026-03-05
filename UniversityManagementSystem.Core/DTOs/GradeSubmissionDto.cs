using System.ComponentModel.DataAnnotations;
using NUlid;

namespace UniversityManagementSystem.Core.DTOs
{
    public class GradeSubmissionDto
    {
        [Required]
        public Ulid SubmissionId { get; set; }

        [Required]
        [Range(0, 1000)] // Reasonable max score
        public double Score { get; set; }
    }
}
