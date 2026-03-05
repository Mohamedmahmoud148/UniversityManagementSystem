using System.Threading.Tasks;
using NUlid;

namespace UniversityManagementSystem.Core.Interfaces
{
    public interface IBulkUploadJob
    {
        Task ProcessStudentDirectUpload(Ulid fileId, Ulid uploaderUserId);
        Task ProcessStudentAiUpload(Ulid fileId, Ulid uploaderUserId);
        Task ProcessDoctorUpload(Ulid fileId, Ulid uploaderUserId);
    }
}
