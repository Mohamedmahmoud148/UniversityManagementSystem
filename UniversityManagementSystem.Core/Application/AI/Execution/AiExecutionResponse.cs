namespace UniversityManagementSystem.Core.Application.AI.Execution;

public class AiExecutionResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public object? Data { get; set; }
}
