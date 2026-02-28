namespace UniversityManagementSystem.Core.Application.AI.Security;

public static class AiCapabilityMatrix
{
    private static readonly Dictionary<string, List<string>> _roleCapabilities = new();

    public static bool IsAllowed(string role, string toolName)
    {
        if (_roleCapabilities.TryGetValue(role, out var allowedTools))
        {
            return allowedTools.Contains(toolName);
        }
        return false;
    }
}
