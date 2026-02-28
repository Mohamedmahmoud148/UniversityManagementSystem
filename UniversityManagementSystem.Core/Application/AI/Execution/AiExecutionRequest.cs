namespace UniversityManagementSystem.Core.Application.AI.Execution;

public class AiExecutionRequest
{
    public string ToolName { get; set; } = string.Empty;
    public Dictionary<string, object> Parameters { get; set; } = new();
}
