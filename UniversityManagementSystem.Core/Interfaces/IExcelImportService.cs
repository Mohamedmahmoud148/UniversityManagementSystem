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
    }
}

