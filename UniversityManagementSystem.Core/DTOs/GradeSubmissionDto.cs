using System.ComponentModel.DataAnnotations;

namespace UniversityManagementSystem.Core.DTOs
{
    public class GradeSubmissionDto
    {
        [Required]
        public int SubmissionId { get; set; }

        [Required]
        [Range(0, 1000)] // Reasonable max score
        public double Score { get; set; }
    }
}
