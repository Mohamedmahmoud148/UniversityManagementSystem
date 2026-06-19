using System;
using System.ComponentModel.DataAnnotations.Schema;
using NUlid;

namespace UniversityManagementSystem.Core.Entities
{
    public enum AuditSeverity { Info, Warning, Error, Critical, Security }

    /// <summary>
    /// Standalone audit log — not a BaseEntity, never soft-deleted, append-only.
    /// </summary>
    public class AuditLog
    {
        public Ulid Id { get; set; } = Ulid.NewUlid();
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        // ── Who ──────────────────────────────────────────────────────────────
        public Ulid? UserId { get; set; }
        public string? UserName { get; set; }
        public string? Email { get; set; }
        public string? Role { get; set; }

        // ── What ─────────────────────────────────────────────────────────────
        public string Action { get; set; } = string.Empty;
        public string Entity { get; set; } = string.Empty;
        public string EntityId { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        // ── Severity / Status ─────────────────────────────────────────────────
        public AuditSeverity Severity { get; set; } = AuditSeverity.Info;
        public string Status { get; set; } = "Success";

        // ── Context ───────────────────────────────────────────────────────────
        public string? IpAddress { get; set; }
        public string? UserAgent { get; set; }
        public string? Browser { get; set; }
        public string? Device { get; set; }

        // ── Tracking ─────────────────────────────────────────────────────────
        public string? CorrelationId { get; set; }
        public string? RequestId { get; set; }
        public long? DurationMs { get; set; }

        // ── Changes ───────────────────────────────────────────────────────────
        public string? OldValues { get; set; }
        public string? NewValues { get; set; }
        public string? ChangedFields { get; set; }
        public string? Metadata { get; set; }

        // ── Backwards-compat aliases (not mapped to DB columns) ───────────────
        [NotMapped] public string ActionType { get => Action; set => Action = value; }
        [NotMapped] public string EntityName { get => Entity; set => Entity = value; }
        [NotMapped] public Ulid? PerformedByUserId { get => UserId; set => UserId = value; }
        [NotMapped] public DateTime PerformedAt { get => Timestamp; set => Timestamp = value; }
    }
}
