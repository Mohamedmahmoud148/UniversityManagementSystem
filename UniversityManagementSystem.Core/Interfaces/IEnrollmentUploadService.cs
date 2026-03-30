using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using NUlid;
using UniversityManagementSystem.Core.DTOs;

namespace UniversityManagementSystem.Core.Interfaces
{
    public interface IEnrollmentUploadService
    {
        /// <summary>
        /// Uploads the Excel file to "enrollment/" in R2, parses each row,
        /// resolves codes to IDs, creates students, and returns a result summary.
        /// </summary>
        Task<EnrollmentUploadResultDto> ProcessExcelAsync(Ulid adminId, IFormFile file);
    }
}
