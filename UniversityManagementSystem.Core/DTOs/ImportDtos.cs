using NUlid;

namespace UniversityManagementSystem.Core.DTOs
{
    public class StudentImportDto
    {
        public string FullName { get; set; } = string.Empty;
        public string NationalId { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public Ulid BatchId { get; set; }
        /// <summary>Raw value from column 4 — used as fallback to look up batch by Name or Code.</summary>
        public string BatchRaw { get; set; } = string.Empty;
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
        /// <summary>
        /// Temporary password assigned to all imported users during this import.
        /// Only returned once — not retrievable after this response.
        /// </summary>
        public string TemporaryPassword { get; set; } = "123456";
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

