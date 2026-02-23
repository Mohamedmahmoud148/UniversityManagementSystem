using Microsoft.AspNetCore.Http;

namespace UniversityManagementSystem.Core.DTOs
{
    public class UploadMaterialDto
    {
        public int OfferingId { get; set; }
        public IFormFile File { get; set; } = null!;
    }
}
