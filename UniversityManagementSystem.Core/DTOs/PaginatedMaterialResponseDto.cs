using System.Collections.Generic;

namespace UniversityManagementSystem.Core.DTOs
{
    public class PaginatedMaterialResponseDto
    {
        public int TotalCount { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public IEnumerable<MaterialDto> Items { get; set; } = [];
    }
}
