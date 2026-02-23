using System;

namespace UniversityManagementSystem.Core.Entities
{
    public class AiMemory : BaseEntity
    {
        public int UserId { get; set; }
        public string Fact { get; set; } = string.Empty;
        public string Context { get; set; } = string.Empty; // e.g. "Personal Preference", "Academic History"
        public float ConfidenceScore { get; set; }

        // Navigation Properties
        public SystemUser User { get; set; } = null!;
    }
}
