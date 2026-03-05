using System;
using NUlid;

namespace UniversityManagementSystem.Core.Entities
{
    public class AiMemory : BaseEntity
    {
        public Ulid UserId { get; set; }
        public string Fact { get; set; } = string.Empty;
        public string Context { get; set; } = string.Empty;
        public float ConfidenceScore { get; set; }

        // Navigation Properties
        public SystemUser User { get; set; } = null!;
    }
}
