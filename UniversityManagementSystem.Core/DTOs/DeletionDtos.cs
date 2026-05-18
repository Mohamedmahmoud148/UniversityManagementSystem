using System.Collections.Generic;

namespace UniversityManagementSystem.Core.DTOs
{
    // ── Enums ────────────────────────────────────────────────────────────────

    public enum DeleteRiskLevel
    {
        Low = 0,
        Medium = 1,
        High = 2,
        Critical = 3,
        Catastrophic = 4
    }

    public enum DeleteType
    {
        SoftDelete,
        HardDelete,
        Restricted,
        ArchiveOnly,
        ImmutableBlocked
    }

    // ── Analysis Request ─────────────────────────────────────────────────────

    public class DeleteAnalysisRequestDto
    {
        public string EntityName { get; set; } = string.Empty;
        public string EntityId   { get; set; } = string.Empty;
    }

    // ── Dependency Node (recursive tree) ────────────────────────────────────

    public class DependencyNodeDto
    {
        public string EntityName        { get; set; } = string.Empty;
        public string FriendlyName      { get; set; } = string.Empty;
        public int    Count             { get; set; }
        public bool   IsHistorical      { get; set; }
        public bool   IsBlocking        { get; set; }
        public string DeleteBehavior    { get; set; } = string.Empty; // "Cascade" | "Restrict" | "SoftDelete"
        public List<DependencyNodeDto> Children { get; set; } = new();
    }

    // ── Blocker (hard stops) ─────────────────────────────────────────────────

    public class DeleteBlockerDto
    {
        public string Reason     { get; set; } = string.Empty;
        public string EntityName { get; set; } = string.Empty;
        public int    Count      { get; set; }
    }

    // ── Confirmation requirements ────────────────────────────────────────────

    public class ConfirmationRequirementsDto
    {
        public bool   RequiresTypedConfirmation    { get; set; }
        public string TypedConfirmationPhrase      { get; set; } = string.Empty;
        public bool   RequiresPasswordConfirmation { get; set; }
        public bool   RequiresSecondAdminApproval  { get; set; }
        public int    ConfirmationSteps            { get; set; }
    }

    // ── Affected summary (flat counts) ──────────────────────────────────────

    public class AffectedSummaryDto
    {
        public Dictionary<string, int> Counts { get; set; } = new();
    }

    // ── Full analysis response ───────────────────────────────────────────────

    public class DeleteAnalysisResponseDto
    {
        public string                    EntityName              { get; set; } = string.Empty;
        public string                    EntityId                { get; set; } = string.Empty;
        public string                    DisplayName             { get; set; } = string.Empty;
        public DeleteRiskLevel           RiskLevel               { get; set; }
        public string                    RiskLevelLabel          { get; set; } = string.Empty;
        public DeleteType                DeleteType              { get; set; }
        public string                    DeleteTypeLabel         { get; set; } = string.Empty;
        public bool                      CanDelete               { get; set; }
        public bool                      IsBlocked               { get; set; }
        public AffectedSummaryDto        Summary                 { get; set; } = new();
        public List<DependencyNodeDto>   DependencyTree          { get; set; } = new();
        public List<string>              Warnings                { get; set; } = new();
        public List<DeleteBlockerDto>    Blockers                { get; set; } = new();
        public ConfirmationRequirementsDto Confirmation          { get; set; } = new();
        public List<string>              DeletionOrder           { get; set; } = new();
    }

    // ── Execution request ────────────────────────────────────────────────────

    public class DeleteExecutionRequestDto
    {
        public string  EntityName                  { get; set; } = string.Empty;
        public string  EntityId                    { get; set; } = string.Empty;
        public string? TypedConfirmationPhrase     { get; set; }
        public string? AdminPassword               { get; set; }
        public string? SecondAdminApprovalToken    { get; set; }
    }

    // ── Execution response ───────────────────────────────────────────────────

    public class DeleteExecutionResponseDto
    {
        public bool         Success           { get; set; }
        public string       Message           { get; set; } = string.Empty;
        public string       EntityName        { get; set; } = string.Empty;
        public string       EntityId          { get; set; } = string.Empty;
        public DeleteType   DeleteTypeApplied { get; set; }
        public Dictionary<string, int> AffectedCounts { get; set; } = new();
        public List<string> ExecutedSteps     { get; set; } = new();
    }
}
