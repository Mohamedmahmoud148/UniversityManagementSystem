using System.Threading.Tasks;

namespace UniversityManagementSystem.Core.Interfaces
{
    public interface IExamReminderJob
    {
        Task RunAsync();
    }
}
