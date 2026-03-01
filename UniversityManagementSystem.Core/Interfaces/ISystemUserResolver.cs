using System.Security.Claims;
using System.Threading.Tasks;

namespace UniversityManagementSystem.Core.Interfaces
{
    public interface ISystemUserResolver
    {
        Task<int> ResolveSystemUserIdAsync(ClaimsPrincipal user);
    }
}
