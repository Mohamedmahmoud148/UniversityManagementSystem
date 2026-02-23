using System.ComponentModel.DataAnnotations;

namespace UniversityManagementSystem.Core.DTOs
{
    public class AcademicYearDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool IsActive { get; set; }
    }

    public class CreateAcademicYearDto
    {
        [Required]
        public string Name { get; set; } = string.Empty;
        
        public bool IsActive { get; set; }
    }

    public class UpdateAcademicYearDto
    {
        [Required]
        public string Name { get; set; } = string.Empty;
        
        public bool IsActive { get; set; }
    }
}
