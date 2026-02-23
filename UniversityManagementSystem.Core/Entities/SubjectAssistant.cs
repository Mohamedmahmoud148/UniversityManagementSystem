using System;

namespace UniversityManagementSystem.Core.Entities
{
    public class SubjectAssistant
    {
        public int SubjectId { get; set; }
        public int TeachingAssistantId { get; set; }

        // Navigation Properties
        public Subject Subject { get; set; } = null!;
        public TeachingAssistant TeachingAssistant { get; set; } = null!;
    }
}
