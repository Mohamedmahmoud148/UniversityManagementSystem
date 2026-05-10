using NUlid;

namespace UniversityManagementSystem.Core.DTOs
{
    public class StudentImportDto
    {
        public string FullName { get; set; } = string.Empty;
        public string NationalId { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public Ulid BatchId { get; set; }
    }

    public class DoctorImportDto
    {
        public string FullName { get; set; } = string.Empty;
        public string NationalId { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
    }

    /// <summary>Response DTO for POST /api/students/import-excel.</summary>
    public class ImportStudentsResultDto
    {
        public int TotalRows { get; set; }
        public int Imported { get; set; }
        public int Skipped { get; set; }
        public List<string> Errors { get; set; } = new();
        /// <summary>Non-fatal notices (missing optional columns, auto-generated IDs, invalid phone fallback).</summary>
        public List<string> Warnings { get; set; } = new();
    }

    public class ImportGradesResultDto
    {
        public int TotalRows { get; set; }
        public int Imported { get; set; }
        public int Skipped { get; set; }
        public List<string> Errors { get; set; } = new();
        public string UploadedFileId { get; set; } = string.Empty;
    }
}

