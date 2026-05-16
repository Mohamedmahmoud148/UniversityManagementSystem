using System.Threading.Tasks;

namespace UniversityManagementSystem.Core.Interfaces
{
    /// <summary>
    /// Abstraction over SignalR so Infrastructure does not depend on the Api project.
    /// </summary>
    public interface IRealtimeNotifier
    {
        Task PushToUserAsync(string userId, string title, string message, string? actionUrl = null);
    }
}
