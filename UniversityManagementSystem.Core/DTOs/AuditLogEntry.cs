using NUlid;
using UniversityManagementSystem.Core.Entities;

namespace UniversityManagementSystem.Core.DTOs
{
    public class AuditLogEntry
    {
        public string Action { get; set; } = string.Empty;
        public string Entity { get; set; } = string.Empty;
        public string EntityId { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public AuditSeverity Severity { get; set; } = AuditSeverity.Info;
        public string Status { get; set; } = "Success";
        public Ulid? UserId { get; set; }
        public string? UserName { get; set; }
        public string? Email { get; set; }
        public string? Role { get; set; }
        public string? IpAddress { get; set; }
        public string? UserAgent { get; set; }
        public string? Browser { get; set; }
        public string? Device { get; set; }
        public string? CorrelationId { get; set; }
        public string? OldValues { get; set; }
        public string? NewValues { get; set; }
        public string? ChangedFields { get; set; }
        public string? Metadata { get; set; }
        public long? DurationMs { get; set; }
    }
}
