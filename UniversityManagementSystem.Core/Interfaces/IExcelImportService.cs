using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;
using UniversityManagementSystem.Core.DTOs; // We might need a generic DTO or Result

namespace UniversityManagementSystem.Core.Interfaces
{
    public interface IExcelImportService
    {
        Task<ExcelImportResultDto> ImportStudentsAsync(IFormFile file);
    }
}
