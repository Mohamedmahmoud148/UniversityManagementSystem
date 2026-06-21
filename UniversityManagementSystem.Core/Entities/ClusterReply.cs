using System;
using NUlid;

namespace UniversityManagementSystem.Core.Entities
{
    public class ClusterReply : BaseEntity
    {
        public Ulid ClusterId { get; set; }
        public Ulid RepliedByUserId { get; set; }
        public string Message { get; set; } = string.Empty;
        public int AffectedStudents { get; set; }
        public int NotificationsSent { get; set; }
        public ComplaintCluster Cluster { get; set; } = null!;
    }
}
