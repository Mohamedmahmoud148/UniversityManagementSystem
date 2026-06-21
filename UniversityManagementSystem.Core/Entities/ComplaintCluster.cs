using System;
using System.Collections.Generic;
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

        // ── Enhancement fields ────────────────────────────────────────────────
        public string Status { get; set; } = "Open"; // Open | Investigating | Resolved | Archived
        public string? AiRecommendations { get; set; } // JSON array as string
        public int CriticalCount { get; set; }
        public double AverageSentiment { get; set; }
        public DateTime? ResolvedAt { get; set; }
        public DateTime FirstComplaintAt { get; set; } = DateTime.UtcNow;
        public string TrendDirection { get; set; } = "Stable"; // Increasing | Stable | Decreasing
        public List<ClusterReply> Replies { get; set; } = new();
        public List<ClusterStatusHistory> StatusHistory { get; set; } = new();
    }
}
