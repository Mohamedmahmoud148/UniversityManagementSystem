using System.Threading.Tasks;

namespace UniversityManagementSystem.Core.Interfaces
{
    public interface IAssignmentReminderJob
    {
        Task RunAsync();
    }
}
