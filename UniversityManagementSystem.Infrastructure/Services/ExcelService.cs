using ClosedXML.Excel;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UniversityManagementSystem.Core.DTOs;
using UniversityManagementSystem.Core.Interfaces;

namespace UniversityManagementSystem.Infrastructure.Services
{
    public class ExcelService : IExcelService
    {
        public IEnumerable<StudentImportDto> ParseStudents(Stream stream)
        {
            var list = new List<StudentImportDto>();
            using var workbook = new XLWorkbook(stream);
            var worksheet = workbook.Worksheet(1);
            var range = worksheet.RangeUsed();
            if (range == null) return list;
            var rows = range.RowsUsed().Skip(1); // Skip header

            foreach (var row in rows)
            {
                list.Add(new StudentImportDto
                {
                    FullName = row.Cell(1).GetValue<string>(),
                    NationalId = row.Cell(2).GetValue<string>(),
                    Phone = row.Cell(3).GetValue<string>(),
                    BatchId = row.Cell(4).GetValue<int>(),
                });
            }
            return list;
        }

        public IEnumerable<DoctorImportDto> ParseDoctors(Stream stream)
        {
            var list = new List<DoctorImportDto>();
            using var workbook = new XLWorkbook(stream);
            var worksheet = workbook.Worksheet(1);
            var range = worksheet.RangeUsed();
            if (range == null) return list;
            var rows = range.RowsUsed().Skip(1); // Skip header

            foreach (var row in rows)
            {
                list.Add(new DoctorImportDto
                {
                    FullName = row.Cell(1).GetValue<string>(),
                    NationalId = row.Cell(2).GetValue<string>(),
                    Phone = row.Cell(3).GetValue<string>(),
                    Department = row.Cell(4).GetValue<string>(),
                });
            }
            return list;
        }
    }
}
