using ClosedXML.Excel;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UniversityManagementSystem.Core.DTOs;
using UniversityManagementSystem.Core.Entities;
using UniversityManagementSystem.Core.Interfaces;
using UniversityManagementSystem.Infrastructure.Data;
using NUlid;

namespace UniversityManagementSystem.Infrastructure.Services
{
    public class ExcelImportService(AppDbContext context, IIdentityProvisioningService provisioningService) : IExcelImportService
    {
        private readonly AppDbContext _context = context;
        private readonly IIdentityProvisioningService _provisioningService = provisioningService;

        // ── Existing method (kept intact) ─────────────────────────────────────
        public async Task<ExcelImportResultDto> ImportStudentsAsync(IFormFile file)
        {
            var result = new ExcelImportResultDto();

            if (file == null || file.Length == 0)
            {
                result.Errors.Add("File is empty or null.");
                return result;
            }

            using var stream = new MemoryStream();
            await file.CopyToAsync(stream);
            using var workbook = new XLWorkbook(stream);
            var worksheet = workbook.Worksheet(1);
            var range = worksheet.RangeUsed();
            if (range == null)
            {
                result.Errors.Add("Worksheet is empty.");
                return result;
            }
            var rows = range.RowsUsed().Skip(1);

            result.TotalRows = rows.Count();

            var newUsers = new List<SystemUser>();
            var newStudents = new List<Student>();

            var existingNationalIds = await _context.SystemUsers.Select(u => u.NationalId).ToListAsync();
            var existingBatches = await _context.Batches.Select(b => b.Id).ToListAsync();

            foreach (var row in rows)
            {
                try
                {
                    string fullName = row.Cell(1).GetValue<string>();
                    string nationalId = row.Cell(2).GetValue<string>();
                    string phone = row.Cell(3).GetValue<string>();
                    Ulid batchId = Ulid.TryParse(row.Cell(4).GetValue<string>(), out var bId) ? bId : Ulid.Empty;

                    if (existingNationalIds.Contains(nationalId))
                    {
                        result.Failed++;
                        result.Errors.Add($"Row {row.RowNumber()}: National ID {nationalId} already exists.");
                        continue;
                    }

                    if (!existingBatches.Contains(batchId))
                    {
                        result.Failed++;
                        result.Errors.Add($"Row {row.RowNumber()}: Batch ID {batchId} does not exist.");
                        continue;
                    }

                    if (newUsers.Any(u => u.NationalId == nationalId))
                    {
                        result.Failed++;
                        result.Errors.Add($"Row {row.RowNumber()}: Duplicate National ID {nationalId} in file.");
                        continue;
                    }

                    string password = "TempPassword123!";
                    string universityEmail = $"student.{nationalId[8..]}@university.edu";

                    var user = new SystemUser
                    {
                        Id = Ulid.NewUlid(),
                        FullName = fullName,
                        NationalId = nationalId,
                        Email = universityEmail,
                        UniversityEmail = universityEmail,
                        PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                        Role = UserRole.Student,
                        IsActive = true
                    };

                    var student = new Student
                    {
                        Id = Ulid.NewUlid(),
                        FullName = fullName,
                        Phone = phone,
                        BatchId = batchId,
                        SystemUser = user
                    };

                    newUsers.Add(user);
                    existingNationalIds.Add(nationalId);
                    result.Inserted++;
                }
                catch (Exception ex)
                {
                    result.Failed++;
                    result.Errors.Add($"Row {row.RowNumber()}: {ex.Message}");
                }
            }

            return result;
        }

        // ── New method: POST /api/students/import-excel ───────────────────────
        /// <summary>
        /// Parses an .xlsx file with columns:
        ///   1=FullName | 2=Email | 3=UniversityStudentId | 4=BatchCode | 5=GroupCode
        ///
        /// Strategy:
        ///   • Pre-fetches all Batches (with Dept→College→University chain) and Groups
        ///     into dictionaries — zero per-row DB round trips for lookups.
        ///   • Pre-fetches existing Email and UniversityStudentId sets for dedup.
        ///   • Also tracks duplicates within the uploaded file itself.
        ///   • Builds all valid Student + SystemUser pairs, then performs a single
        ///     SaveChangesAsync call (O(1) round trips regardless of file size).
        /// </summary>
        public async Task<ImportStudentsResultDto> ImportStudentsFromExcelAsync(IFormFile file)
        {
            var result = new ImportStudentsResultDto();

            // ── 1. File validation ─────────────────────────────────────────
            if (file == null || file.Length == 0)
                return Fail(result, "Uploaded file is empty or missing.");

            var ext = Path.GetExtension(file.FileName);
            if (!ext.Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
                return Fail(result, $"Invalid file format '{ext}'. Only .xlsx files are accepted.");

            // ── 2. Open workbook ───────────────────────────────────────────
            using var memStream = new MemoryStream();
            await file.CopyToAsync(memStream);
            memStream.Position = 0;

            using var workbook = new XLWorkbook(memStream);
            var worksheet = workbook.Worksheet(1);  // first sheet
            var range = worksheet.RangeUsed();

            if (range == null)
                return Fail(result, "The worksheet is empty.");

            // Skip header row (row 1), materialise remaining rows
            var dataRows = range.RowsUsed().Skip(1).ToList();
            result.TotalRows = dataRows.Count;

            if (result.TotalRows == 0)
            {
                result.Errors.Add("No data rows found (only header present).");
                return result;
            }

            // ── 3. Pre-fetch lookup data (eliminates N+1 DB hits) ──────────

            // Batches keyed by Code; eagerly load full FK chain
            var batchesByCode = await _context.Batches
                .AsNoTracking()
                .Include(b => b.Department)
                    .ThenInclude(d => d.College)
                        .ThenInclude(c => c.University)
                .ToDictionaryAsync(b => b.Code, StringComparer.OrdinalIgnoreCase);

            // Groups keyed by Code
            var groupsByCode = await _context.Groups
                .AsNoTracking()
                .ToDictionaryAsync(g => g.Code, StringComparer.OrdinalIgnoreCase);

            // Existing student Email and UniversityStudentId sets (for duplicate detection)
            var existingEmails = await _context.Students
                .AsNoTracking()
                .Select(s => s.Email.ToLower())
                .ToHashSetAsync();

            var existingStudentIds = await _context.Students
                .AsNoTracking()
                .Select(s => s.UniversityStudentId.ToLower())
                .ToHashSetAsync();

            // In-batch dedup sets (catches duplicates within the same file)
            var batchEmails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var batchStudentIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // ── 4. Row-by-row parsing ──────────────────────────────────────
            var newStudents = new List<Student>();

            foreach (var row in dataRows)
            {
                int rowNum = row.RowNumber();

                // Extract and trim each cell
                string fullName = row.Cell(1).GetValue<string>().Trim();
                string email = row.Cell(2).GetValue<string>().Trim();
                string universityStId = row.Cell(3).GetValue<string>().Trim();
                string batchCode = row.Cell(4).GetValue<string>().Trim();
                string groupCode = row.Cell(5).GetValue<string>().Trim();

                // ── 4a. Required field check ──────────────────────────────
                if (string.IsNullOrWhiteSpace(fullName) ||
                    string.IsNullOrWhiteSpace(email) ||
                    string.IsNullOrWhiteSpace(universityStId) ||
                    string.IsNullOrWhiteSpace(batchCode) ||
                    string.IsNullOrWhiteSpace(groupCode))
                {
                    result.Skipped++;
                    result.Errors.Add($"Row {rowNum}: One or more required fields are empty — skipped.");
                    continue;
                }

                // ── 5. Resolve Batch by Code ──────────────────────────────
                if (!batchesByCode.TryGetValue(batchCode, out var batch))
                {
                    result.Skipped++;
                    result.Errors.Add($"Row {rowNum}: Batch '{batchCode}' not found — skipped.");
                    continue;
                }

                // ── 5b. Resolve Group by Code (must belong to resolved batch)
                if (!groupsByCode.TryGetValue(groupCode, out var group))
                {
                    result.Skipped++;
                    result.Errors.Add($"Row {rowNum}: Group '{groupCode}' not found — skipped.");
                    continue;
                }

                if (group.BatchId != batch.Id)
                {
                    result.Skipped++;
                    result.Errors.Add($"Row {rowNum}: Group '{groupCode}' does not belong to batch '{batchCode}' — skipped.");
                    continue;
                }

                // ── 6. Duplicate detection ────────────────────────────────
                if (existingEmails.Contains(email.ToLower()) || batchEmails.Contains(email))
                {
                    result.Skipped++;
                    result.Errors.Add($"Row {rowNum}: Duplicate email '{email}' — skipped.");
                    continue;
                }

                if (existingStudentIds.Contains(universityStId.ToLower()) || batchStudentIds.Contains(universityStId))
                {
                    result.Skipped++;
                    result.Errors.Add($"Row {rowNum}: Duplicate UniversityStudentId '{universityStId}' — skipped.");
                    continue;
                }

                // ── 7. Derive required FKs from Batch navigation chain ────
                //   Batch.DepartmentId  → Department.CollegeId → College.UniversityId
                var department = batch.Department;
                var college = department.College;
                var university = college.University;

                try
                {
                    // Build SystemUser — personal email from Excel, university email generated
                    var systemUser = new SystemUser
                    {
                        Id = Ulid.NewUlid(),
                        FullName = fullName,
                        Email = email,
                        UniversityEmail = $"{universityStId.ToLower()}@university.edu",
                        NationalId = string.Empty,          // not provided in this Excel format
                        PasswordHash = BCrypt.Net.BCrypt.HashPassword("TempPass@123"),
                        Role = UserRole.Student,
                        IsActive = true
                    };

                    // Build Student — Phone not in Excel, set placeholder (update after import)
                    var student = new Student
                    {
                        Id = Ulid.NewUlid(),
                        FullName = fullName,
                        Email = email,
                        UniversityStudentId = universityStId,
                        Phone = "01000000000",     // placeholder: ^01[0125][0-9]{8}$ ✓
                        UniversityId = university.Id,
                        CollegeId = college.Id,
                        DepartmentId = department.Id,
                        BatchId = batch.Id,
                        GroupId = group.Id,
                        IsActive = true,
                        SystemUser = systemUser         // EF inserts SystemUser first, then Student
                    };

                    newStudents.Add(student);

                    // Update in-batch dedup sets so the next row detects dupes within the file
                    batchEmails.Add(email);
                    batchStudentIds.Add(universityStId);

                    result.Imported++;
                }
                catch (Core.Exceptions.DomainException dex)
                {
                    // Entity setter validation (e.g. FullName regex, Phone regex)
                    result.Skipped++;
                    result.Errors.Add($"Row {rowNum}: Validation error — {dex.Message} — skipped.");
                }
                catch (Exception ex)
                {
                    result.Skipped++;
                    result.Errors.Add($"Row {rowNum}: Unexpected error — {ex.Message} — skipped.");
                }
            }

            // ── 8. Single bulk insert (one SaveChangesAsync for all valid rows) ──
            // EF Core automatically inserts referenced SystemUser objects first,
            // satisfying the FK constraint before inserting Student rows.
            if (newStudents.Count > 0)
            {
                _context.Students.AddRange(newStudents);
                await _context.SaveChangesAsync();
            }

            return result;
        }

        // ── Private helper ────────────────────────────────────────────────────
        private static ImportStudentsResultDto Fail(ImportStudentsResultDto result, string message)
        {
            result.Errors.Add(message);
            return result;
        }

        // ── New method: Import Offline Grades ─────────────────────────────────
        public async Task<ImportGradesResultDto> ImportGradesFromExcelAsync(Ulid offeringId, Ulid doctorId, IFormFile file)
        {
            var result = new ImportGradesResultDto();

            if (file == null || file.Length == 0)
            {
                result.Errors.Add("Uploaded file is empty or missing.");
                return result;
            }

            var ext = Path.GetExtension(file.FileName);
            if (!ext.Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
            {
                result.Errors.Add($"Invalid file format '{ext}'. Only .xlsx files are accepted.");
                return result;
            }

            // Verify doctor ownership
            var offering = await _context.Set<SubjectOffering>().AsNoTracking().FirstOrDefaultAsync(o => o.Id == offeringId);
            if (offering == null)
            {
                result.Errors.Add("Subject offering not found.");
                return result;
            }
            if (offering.DoctorId != doctorId)
            {
                result.Errors.Add("You are not the instructor for this offering.");
                return result;
            }

            using var memStream = new MemoryStream();
            await file.CopyToAsync(memStream);
            memStream.Position = 0;

            using var workbook = new XLWorkbook(memStream);
            var worksheet = workbook.Worksheet(1);
            var range = worksheet.RangeUsed();

            if (range == null)
            {
                result.Errors.Add("The worksheet is empty.");
                return result;
            }

            var headerRow = range.FirstRow();
            var dataRows = range.RowsUsed().Skip(1).ToList();
            result.TotalRows = dataRows.Count;

            if (result.TotalRows == 0)
            {
                result.Errors.Add("No data rows found (only header present).");
                return result;
            }

            // Dynamic Column Mapping (Case Insensitive)
            int idCol = -1, midtermCol = -1, courseworkCol = -1, finalCol = -1;
            foreach (var cell in headerRow.Cells())
            {
                var val = cell.GetValue<string>().ToLower().Trim();
                if (val.Contains("id") || val.Contains("student")) idCol = cell.Address.ColumnNumber;
                else if (val.Contains("midterm")) midtermCol = cell.Address.ColumnNumber;
                else if (val.Contains("coursework") || val.Contains("work")) courseworkCol = cell.Address.ColumnNumber;
                else if (val.Contains("final")) finalCol = cell.Address.ColumnNumber;
            }

            if (idCol == -1)
            {
                result.Errors.Add("Could not identify the Student ID column. Ensure the header contains 'id' or 'student'.");
                return result;
            }

            // Pre-fetch Enrolled Students mapping (UniversityStudentId -> StudentId)
            var enrolledStudents = await _context.Enrollments
                .Include(e => e.Student)
                .Where(e => e.SubjectOfferingId == offeringId)
                .ToDictionaryAsync(e => e.Student.UniversityStudentId.ToLower(), e => e.StudentId);

            // Fetch existing Grade Records
            var existingGrades = await _context.Set<StudentGrade>()
                .IgnoreQueryFilters()
                .Where(g => g.SubjectOfferingId == offeringId)
                .ToDictionaryAsync(g => g.StudentId);

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                foreach (var row in dataRows)
                {
                    int rowNum = row.RowNumber();
                    string uniId = row.Cell(idCol).GetValue<string>().Trim().ToLower();

                    if (string.IsNullOrWhiteSpace(uniId))
                    {
                        result.Skipped++;
                        result.Errors.Add($"Row {rowNum}: Student ID is missing.");
                        continue;
                    }

                    if (!enrolledStudents.TryGetValue(uniId, out var studentId))
                    {
                        result.Skipped++;
                        result.Errors.Add($"Row {rowNum}: Student '{uniId}' is not enrolled in this offering.");
                        continue;
                    }

                    double? ParseDouble(int colIndex)
                    {
                        if (colIndex == -1) return null;
                        var cellStr = row.Cell(colIndex).GetValue<string>().Trim();
                        if (string.IsNullOrWhiteSpace(cellStr)) return null;
                        if (double.TryParse(cellStr, out var d)) return d;
                        return null; // Invalid format treated as null (or could be an error, but null is safer for partial grades)
                    }

                    double? midterm = ParseDouble(midtermCol);
                    double? coursework = ParseDouble(courseworkCol);
                    double? final = ParseDouble(finalCol);

                    // Validations
                    if (midterm.HasValue && midterm.Value > offering.MidtermMaxScore)
                    {
                        result.Skipped++;
                        result.Errors.Add($"Row {rowNum}: Midterm score {midterm.Value} exceeds max {offering.MidtermMaxScore}.");
                        continue;
                    }
                    if (coursework.HasValue && coursework.Value > offering.CourseworkMaxScore)
                    {
                        result.Skipped++;
                        result.Errors.Add($"Row {rowNum}: Coursework score {coursework.Value} exceeds max {offering.CourseworkMaxScore}.");
                        continue;
                    }
                    if (final.HasValue && final.Value > offering.FinalExamMaxScore)
                    {
                        result.Skipped++;
                        result.Errors.Add($"Row {rowNum}: Final score {final.Value} exceeds max {offering.FinalExamMaxScore}.");
                        continue;
                    }

                    if (!existingGrades.TryGetValue(studentId, out var gradeRecord))
                    {
                        gradeRecord = new StudentGrade
                        {
                            StudentId = studentId,
                            SubjectOfferingId = offeringId,
                        };
                        _context.Set<StudentGrade>().Add(gradeRecord);
                        existingGrades[studentId] = gradeRecord; // track it
                    }
                    else
                    {
                        if (gradeRecord.DeletedAt != null) gradeRecord.DeletedAt = null;
                        _context.Entry(gradeRecord).State = EntityState.Modified;
                    }

                    // Only update if value is present in the sheet
                    if (midterm.HasValue) gradeRecord.MidtermScore = midterm.Value;
                    if (coursework.HasValue) gradeRecord.CourseworkScore = coursework.Value;
                    if (final.HasValue) gradeRecord.FinalExamScore = final.Value;

                    result.Imported++;
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                result.Errors.Add($"Import failed critically and was rolled back: {ex.Message}");
                result.Imported = 0; // Everything rolled back
            }

            return result;
        }
    }
}
