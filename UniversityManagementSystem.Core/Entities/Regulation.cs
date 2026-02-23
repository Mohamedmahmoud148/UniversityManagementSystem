using System;

namespace UniversityManagementSystem.Core.Entities
{
    public class Regulation : BaseEntity
    {
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public RegulationType Type { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public enum RegulationType
    {
        Academic,
        Conduct,
        Exam,
        General
    }
}
