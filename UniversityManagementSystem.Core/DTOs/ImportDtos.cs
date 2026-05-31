using NUlid;

namespace UniversityManagementSystem.Core.DTOs
{
    public class StudentImportDto
    {
        public string FullName   { get; set; } = string.Empty;
        public string NationalId { get; set; } = string.Empty;
        public string Phone      { get; set; } = string.Empty;
        public Ulid   BatchId    { get; set; }
        /// <summary>Raw value — used as fallback to look up batch by Name or Code.</summary>
        public string BatchRaw   { get; set; } = string.Empty;
        /// <summary>Group code for assignment. Column C in bulk upload sheet.</summary>
        public string GroupCode  { get; set; } = string.Empty;
    }

    public class DoctorImportDto
    {
        public string FullName { get; set; } = string.Empty;
        public string NationalId { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
    }

    /// <summary>One row in the credentials export Excel — one per imported student.</summary>
    public class StudentCredentialRow
    {
        public string FullName           { get; set; } = string.Empty;
        public string UniversityStudentId { get; set; } = string.Empty;
        public string UniversityEmail    { get; set; } = string.Empty;
        public string TemporaryPassword  { get; set; } = string.Empty;
        public string BatchCode          { get; set; } = string.Empty;
        public string GroupCode          { get; set; } = string.Empty;
        public string Department         { get; set; } = string.Empty;
    }

    /// <summary>One failed/skipped row captured during import — used in the report Excel.</summary>
    public class FailedImportRow
    {
        public int    RowNumber   { get; set; }
        public string FullName    { get; set; } = string.Empty;
        public string NationalId  { get; set; } = string.Empty;
        public string BatchCode   { get; set; } = string.Empty;
        public string GroupCode   { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
    }

    /// <summary>Response DTO for POST /api/students/import-excel.</summary>
    public class ImportStudentsResultDto
    {
        public int TotalRows        { get; set; }
        public int Imported         { get; set; }
        public int Skipped          { get; set; }
        public int ValidationErrors => Errors.Count;
        public List<string> Errors   { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
        /// <summary>Temporary password for all imported students — only returned once.</summary>
        public string TemporaryPassword { get; set; } = string.Empty;
        /// <summary>Credentials for every successfully imported student — use to generate the download Excel.</summary>
        public List<StudentCredentialRow> ImportedCredentials { get; set; } = new();
        /// <summary>Structured failed rows — used to render the failed-rows sheet in the report Excel.</summary>
        public List<FailedImportRow> FailedRows { get; set; } = new();
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

