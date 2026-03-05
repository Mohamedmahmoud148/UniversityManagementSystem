using Microsoft.AspNetCore.Http;
using NUlid;

namespace UniversityManagementSystem.Core.DTOs
{
    public class UploadMaterialDto
    {
        public Ulid OfferingId { get; set; }
        public IFormFile File { get; set; } = null!;
    }
}
