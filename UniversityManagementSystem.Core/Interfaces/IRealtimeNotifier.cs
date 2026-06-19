using System.Threading.Tasks;

namespace UniversityManagementSystem.Core.Interfaces
{
    public interface IRealtimeNotifier
    {
        Task SendToGroupAsync(string group, string eventName, object data);
        Task SendToUserAsync(string userId, string eventName, object data);
    }
}
