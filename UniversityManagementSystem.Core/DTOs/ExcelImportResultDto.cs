using System.Collections.Generic;

namespace UniversityManagementSystem.Core.DTOs
{
    public class ExcelImportResultDto
    {
        public int TotalRows { get; set; }
        public int Inserted { get; set; }
        public int Failed { get; set; }
        public List<string> Errors { get; set; } = new List<string>();
    }
}
