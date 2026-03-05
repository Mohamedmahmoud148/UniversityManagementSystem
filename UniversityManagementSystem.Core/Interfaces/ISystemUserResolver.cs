using System.Security.Claims;
using System.Threading.Tasks;
using NUlid;

namespace UniversityManagementSystem.Core.Interfaces
{
    public interface ISystemUserResolver
    {
        Task<Ulid> ResolveSystemUserIdAsync(ClaimsPrincipal user);
    }
}
