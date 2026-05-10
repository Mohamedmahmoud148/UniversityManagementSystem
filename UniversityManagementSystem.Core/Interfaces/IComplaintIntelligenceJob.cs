using NUlid;
using System.Threading.Tasks;

namespace UniversityManagementSystem.Core.Interfaces
{
    public interface IComplaintIntelligenceJob
    {
        Task ProcessNewComplaintAsync(Ulid complaintId);
        Task GenerateDailyReportAsync();
        Task GenerateWeeklyReportAsync();
        Task GenerateMonthlyReportAsync();
    }
}
