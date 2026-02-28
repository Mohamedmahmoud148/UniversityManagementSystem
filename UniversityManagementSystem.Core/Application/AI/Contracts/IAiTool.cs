using System.Security.Claims;

namespace UniversityManagementSystem.Core.Application.AI.Contracts;

public interface IAiTool
{
    string Name { get; }
    Task<object> ExecuteAsync(object parameters, ClaimsPrincipal user);
}
