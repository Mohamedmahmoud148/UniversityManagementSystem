using System;
using NUlid;

namespace UniversityManagementSystem.Core.Entities
{
    public class ClusterStatusHistory : BaseEntity
    {
        public Ulid ClusterId { get; set; }
        public string OldStatus { get; set; } = string.Empty;
        public string NewStatus { get; set; } = string.Empty;
        public Ulid ChangedByUserId { get; set; }
        public string? Reason { get; set; }
        public ComplaintCluster Cluster { get; set; } = null!;
    }
}
