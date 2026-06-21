using System;
using System.Collections.Generic;

namespace UniversityManagementSystem.Core.DTOs
{
    // ── Cluster Reply ─────────────────────────────────────────────────────────

    public class ClusterReplyRequestDto
    {
        [System.ComponentModel.DataAnnotations.Required]
        [System.ComponentModel.DataAnnotations.MaxLength(2000)]
        public string Message { get; set; } = string.Empty;
    }

    public class ClusterReplyResponseDto
    {
        public string ClusterId { get; set; } = string.Empty;
        public string Topic { get; set; } = string.Empty;
        public int AffectedStudents { get; set; }
        public int NotificationsSent { get; set; }
        public string Message { get; set; } = string.Empty;
        public DateTime RepliedAt { get; set; }
    }

    // ── Cluster Status ────────────────────────────────────────────────────────

    public class UpdateClusterStatusDto
    {
        [System.ComponentModel.DataAnnotations.Required]
        public string Status { get; set; } = string.Empty; // Open | Investigating | Resolved | Archived
        public string? Reason { get; set; }
    }

    // ── Enhanced Cluster ──────────────────────────────────────────────────────

    public class EnhancedComplaintClusterDto
    {
        public string Id { get; set; } = string.Empty;
        public string Topic { get; set; } = string.Empty;
        public string TargetType { get; set; } = string.Empty;
        public string TargetId { get; set; } = string.Empty;
        public int ComplaintCount { get; set; }
        public int CriticalCount { get; set; }
        public string AiSummary { get; set; } = string.Empty;
        public List<string> AiRecommendations { get; set; } = new();
        public string Status { get; set; } = string.Empty;
        public string TrendDirection { get; set; } = string.Empty;
        public double AverageSentiment { get; set; }
        public DateTime FirstComplaintAt { get; set; }
        public DateTime LastUpdated { get; set; }
        public DateTime? ResolvedAt { get; set; }
        public List<ClusterReplyDto> Replies { get; set; } = new();
        public List<ClusterStatusHistoryDto> StatusHistory { get; set; } = new();
    }

    public class ClusterReplyDto
    {
        public string Id { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public int AffectedStudents { get; set; }
        public int NotificationsSent { get; set; }
        public DateTime RepliedAt { get; set; }
    }

    public class ClusterStatusHistoryDto
    {
        public string OldStatus { get; set; } = string.Empty;
        public string NewStatus { get; set; } = string.Empty;
        public string? Reason { get; set; }
        public DateTime ChangedAt { get; set; }
    }

    // ── Dashboard ─────────────────────────────────────────────────────────────

    public class ComplaintDashboardDto
    {
        public ComplaintSummaryDto Summary { get; set; } = new();
        public List<CategoryCountDto> Categories { get; set; } = new();
        public List<SeverityCountDto> Severities { get; set; } = new();
        public List<EnhancedComplaintClusterDto> TopClusters { get; set; } = new();
        public ComplaintMetricsDto Metrics { get; set; } = new();
        public List<DailyComplaintCountDto> OverTime { get; set; } = new();
    }

    public class ComplaintSummaryDto
    {
        public int TotalComplaints { get; set; }
        public int Pending { get; set; }
        public int UnderReview { get; set; }
        public int Resolved { get; set; }
        public int Dismissed { get; set; }
        public int Critical { get; set; }
        public int TotalClusters { get; set; }
    }

    public class CategoryCountDto
    {
        public string Name { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    public class SeverityCountDto
    {
        public string Severity { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    public class ComplaintMetricsDto
    {
        public double AverageResolutionHours { get; set; }
        public double AverageSentiment { get; set; }
        public int TrendingClustersCount { get; set; }
    }

    public class DailyComplaintCountDto
    {
        public DateTime Date { get; set; }
        public int Count { get; set; }
    }
}
