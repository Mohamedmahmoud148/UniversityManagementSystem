using NUlid;

namespace UniversityManagementSystem.Core.Application.AI.Logging;

public class AiActionLog
{
    public Guid Id { get; set; }
    public Ulid UserId { get; set; }
    public string Role { get; set; } = string.Empty;
    public string ToolName { get; set; } = string.Empty;
    public string ParametersJson { get; set; } = string.Empty;
    public bool Success { get; set; }
    public DateTime Timestamp { get; set; }
}
