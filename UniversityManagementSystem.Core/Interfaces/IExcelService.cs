using System.Collections.Generic;
using System.IO;
using UniversityManagementSystem.Core.DTOs;

namespace UniversityManagementSystem.Core.Interfaces
{
    public interface IExcelService
    {
        IEnumerable<StudentImportDto> ParseStudents(Stream stream);
        IEnumerable<DoctorImportDto> ParseDoctors(Stream stream);
    }
}
