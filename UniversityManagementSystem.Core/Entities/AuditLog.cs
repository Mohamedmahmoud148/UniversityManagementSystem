using System;
using NUlid;

namespace UniversityManagementSystem.Core.Entities
{
    // Standalone audit log — not a BaseEntity so its own Id stays cleanly typed as Ulid
    public class AuditLog
    {
        public Ulid Id { get; set; } = Ulid.NewUlid();
        public string ActionType { get; set; } = string.Empty;
        public string EntityName { get; set; } = string.Empty;
        public string EntityId { get; set; } = string.Empty; // Stored as string (ULID.ToString())
        public string? OldValues { get; set; }
        public string? NewValues { get; set; }
        public Ulid? PerformedByUserId { get; set; }
        public DateTime PerformedAt { get; set; } = DateTime.UtcNow;
    }
}
