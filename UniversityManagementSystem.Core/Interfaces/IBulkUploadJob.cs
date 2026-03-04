using System.Threading.Tasks;

namespace UniversityManagementSystem.Core.Interfaces
{
    public interface IBulkUploadJob
    {
        Task ProcessStudentDirectUpload(int fileId, int uploaderUserId);
        Task ProcessStudentAiUpload(int fileId, int uploaderUserId);
        Task ProcessDoctorUpload(int fileId, int uploaderUserId);
    }
}
