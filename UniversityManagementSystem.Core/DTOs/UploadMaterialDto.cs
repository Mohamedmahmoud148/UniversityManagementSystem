using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using NUlid;

namespace UniversityManagementSystem.Core.DTOs
{
    public class UploadMaterialDto
    {
        [Required]
        public Ulid OfferingId { get; set; }

        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        [MaxLength(1000)]
        public string? Description { get; set; }

        [Required]
        public IFormFile File { get; set; } = null!;
    }
}
