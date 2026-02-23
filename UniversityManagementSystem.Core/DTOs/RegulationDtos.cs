using UniversityManagementSystem.Core.Entities;

namespace UniversityManagementSystem.Core.DTOs
{
    public class RegulationDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public bool IsActive { get; set; }
    }

    public class CreateRegulationDto
    {
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public RegulationType Type { get; set; }
    }

    public class UpdateRegulationDto
    {
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public RegulationType Type { get; set; }
        public bool IsActive { get; set; }
    }
}
