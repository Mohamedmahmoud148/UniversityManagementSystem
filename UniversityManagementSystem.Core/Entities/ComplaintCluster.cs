using System;
using NUlid;

namespace UniversityManagementSystem.Core.Entities
{
    public class ComplaintCluster : BaseEntity
    {
        public string Topic { get; set; } = string.Empty;
        public string TargetType { get; set; } = string.Empty;
        public string TargetId { get; set; } = string.Empty;
        public int ComplaintCount { get; set; }
        public string AiSummary { get; set; } = string.Empty;
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    }
}
