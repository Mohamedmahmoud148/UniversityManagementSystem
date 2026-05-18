using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;
using UniversityManagementSystem.Core.DTOs;

namespace UniversityManagementSystem.Core.Interfaces
{
    public interface IExcelImportService
    {
        Task<ExcelImportResultDto> ImportStudentsAsync(IFormFile file);

        /// <summary>
        /// Imports students from an .xlsx file with columns:
        /// FullName | Email | UniversityStudentId | BatchCode | GroupCode
        /// </summary>
        Task<ImportStudentsResultDto> ImportStudentsFromExcelAsync(IFormFile file);

        /// <summary>
        /// Imports offline grades from an .xlsx file for a specific SubjectOffering.
        /// </summary>
        Task<ImportGradesResultDto> ImportGradesFromExcelAsync(NUlid.Ulid offeringId, NUlid.Ulid doctorId, IFormFile file);

        /// <summary>
        /// Generates a credentials Excel (.xlsx) from imported student credentials.
        /// Returns raw bytes ready for file download response.
        /// </summary>
        Task<byte[]> GenerateCredentialsExcelAsync(IReadOnlyList<StudentCredentialRow> credentials, string universityName);
    }
}

