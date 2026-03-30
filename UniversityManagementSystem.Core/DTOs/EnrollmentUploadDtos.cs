using System.Collections.Generic;

namespace UniversityManagementSystem.Core.DTOs
{
    /// <summary>Returned after processing an enrollment Excel upload.</summary>
    public class EnrollmentUploadResultDto
    {
        public bool Success { get; set; }
        public int CreatedCount { get; set; }
        public int SkippedCount { get; set; }
        public List<string> Errors { get; set; } = new();
        public string UploadId { get; set; } = string.Empty;
    }
}
