using UniversityManagementSystem.Core.Application.AI.Contracts;

namespace UniversityManagementSystem.Core.Application.AI.Execution;

public class AiToolRegistry
{
    private readonly Dictionary<string, IAiTool> _tools;

    public AiToolRegistry(IEnumerable<IAiTool> tools)
    {
        _tools = new Dictionary<string, IAiTool>();
        foreach (var tool in tools)
        {
            _tools[tool.Name] = tool;
        }
    }

    public IAiTool? GetTool(string toolName)
    {
        return _tools.TryGetValue(toolName, out var tool) ? tool : null;
    }
}
