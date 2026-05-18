using ClosedXML.Excel;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UniversityManagementSystem.Core.DTOs;
using UniversityManagementSystem.Core.Entities;
using UniversityManagementSystem.Core.Interfaces;
using UniversityManagementSystem.Core.Settings;
using UniversityManagementSystem.Infrastructure.Data;
using NUlid;

namespace UniversityManagementSystem.Infrastructure.Services
{
    public class ExcelImportService(AppDbContext context, IIdentityProvisioningService provisioningService, IOptions<UniversitySettings> uniOptions) : IExcelImportService
    {
        private readonly AppDbContext _context = context;
        private readonly IIdentityProvisioningService _provisioningService = provisioningService;
        private readonly UniversitySettings _uniSettings = uniOptions.Value;

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

                    string password = _uniSettings.DefaultPassword;
                    string universityEmail = $"student.{nationalId[8..]}@{_uniSettings.StudentEmailDomain}";

                    var user = new SystemUser
                    {
                        Id = Ulid.NewUlid(),
                        FullName = fullName,
                        NationalId = nationalId,
                        Email = universityEmail,
                        UniversityEmail = universityEmail,
                        PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                        Role = UserRole.Student,
                        IsActive = true,
                        MustChangePassword = true
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
                    newStudents.Add(student);
                    existingNationalIds.Add(nationalId);
                    result.Inserted++;
                }
                catch (Exception ex)
                {
                    result.Failed++;
                    result.Errors.Add($"Row {row.RowNumber()}: {ex.Message}");
                }
            }

            // ── BUG FIX: was missing — entities were built but never persisted ──
            if (newStudents.Count > 0)
            {
                _context.Students.AddRange(newStudents);
                await _context.SaveChangesAsync();
            }

            return result;
        }

        // ── New method: POST /api/students/import-excel ───────────────────────
        /// <summary>
        /// Parses an .xlsx file using DYNAMIC header detection.
        ///
        /// REQUIRED columns (case-insensitive, flexible naming):
        ///   FullName  | BatchCode | GroupCode
        ///
        /// OPTIONAL columns (accepted if present, warned if missing):
        ///   NationalId | Phone | Email | UniversityStudentId
        ///
        /// Strategy:
        ///   • Reads the first row as headers — column ORDER doesn't matter.
        ///   • Missing optional columns → warning added, default values used.
        ///   • Missing required columns → import aborted with clear error.
        ///   • Invalid/duplicate rows are SKIPPED and reported, never crash the whole import.
        ///   • Single bulk SaveChangesAsync — O(1) DB round trips.
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
            var worksheet = workbook.Worksheet(1);
            var range = worksheet.RangeUsed();

            if (range == null)
                return Fail(result, "The worksheet is empty.");

            var allRows = range.RowsUsed().ToList();
            if (allRows.Count < 2)
                return Fail(result, "No data rows found (only header present or file is empty).");

            // ── 3. Dynamic header detection ────────────────────────────────
            var headerRow = allRows[0];
            var dataRows = allRows.Skip(1).ToList();
            result.TotalRows = dataRows.Count;

            // Map header name → column number (1-based)
            var colMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var cell in headerRow.Cells())
            {
                var header = cell.GetValue<string>().Trim().ToLower()
                    .Replace(" ", "").Replace("_", "").Replace("-", "");
                if (!string.IsNullOrEmpty(header))
                    colMap[header] = cell.Address.ColumnNumber;
            }

            // Helper: find column by multiple possible aliases
            int FindCol(params string[] aliases)
            {
                foreach (var a in aliases)
                    if (colMap.TryGetValue(a, out var n)) return n;
                return -1;
            }

            int fullNameCol      = FindCol("fullname", "name", "studentname", "اسم");
            int batchCodeCol     = FindCol("batchcode", "batch", "دفعة", "الدفعة");
            int groupCodeCol     = FindCol("groupcode", "group", "مجموعة", "المجموعة");
            int nationalIdCol    = FindCol("nationalid", "national", "رقمقومي");
            int phoneCol         = FindCol("phone", "mobile", "tel", "هاتف", "موبايل");
            int emailCol         = FindCol("email", "personalmail", "mail");
            int uniStudentIdCol  = FindCol("universitystudentid", "studentid", "studentnumber", "رقمطالب");
            // CollegeCode & DepartmentCode are informational — Batch already carries this FK chain
            int collegeCodeCol   = FindCol("collegecode", "college", "كلية", "الكلية");
            int deptCodeCol      = FindCol("departmentcode", "department", "dept", "قسم", "القسم");


            // ── 4. Required column check ───────────────────────────────────
            var missing = new List<string>();
            if (fullNameCol  == -1) missing.Add("FullName");
            if (batchCodeCol == -1) missing.Add("BatchCode");
            if (groupCodeCol == -1) missing.Add("GroupCode");

            if (missing.Count > 0)
                return Fail(result, $"Required columns not found: {string.Join(", ", missing)}. " +
                    "Make sure the first row contains headers (FullName, BatchCode, GroupCode).");

            // Warn about missing optional columns
            if (nationalIdCol   == -1) result.Warnings.Add("Optional column 'NationalId' not found — will be left empty.");
            if (phoneCol        == -1) result.Warnings.Add("Optional column 'Phone' not found — default placeholder will be used.");
            if (emailCol        == -1) result.Warnings.Add("Optional column 'Email' not found — university email will be auto-generated.");
            if (uniStudentIdCol == -1) result.Warnings.Add("Optional column 'UniversityStudentId' not found — will be auto-generated.");

            // ── 5. Pre-fetch lookup data (eliminate N+1) ───────────────────
            var batchesByCode = await _context.Batches
                .AsNoTracking()
                .Include(b => b.Department)
                    .ThenInclude(d => d.College)
                        .ThenInclude(c => c.University)
                .ToDictionaryAsync(b => b.Code, StringComparer.OrdinalIgnoreCase);

            var groupsByCode = await _context.Groups
                .AsNoTracking()
                .ToDictionaryAsync(g => g.Code, StringComparer.OrdinalIgnoreCase);

            var existingNationalIds = await _context.SystemUsers
                .AsNoTracking()
                .Where(u => u.NationalId != null && u.NationalId != "")
                .Select(u => u.NationalId.ToLower())
                .ToHashSetAsync();

            var existingUniStudentIds = await _context.Students
                .AsNoTracking()
                .Select(s => s.UniversityStudentId.ToLower())
                .ToHashSetAsync();

            // Counter for auto-generating UniversityStudentId
            int autoIdCounter = await _context.Students.IgnoreQueryFilters().CountAsync() + 1;

            // In-file dedup
            var batchNationalIds  = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var batchUniStudentIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // ── 6. Row-by-row parsing ──────────────────────────────────────
            var newStudents = new List<Student>();

            string GetCell(IXLRangeRow row, int col)
                => col == -1 ? "" : row.Cell(col).GetValue<string>().Trim();

            foreach (var row in dataRows)
            {
                int rowNum = row.RowNumber();
                try
                {
                    string fullName    = GetCell(row, fullNameCol);
                    string batchCode   = GetCell(row, batchCodeCol);
                    string groupCode   = GetCell(row, groupCodeCol);
                    string nationalId  = GetCell(row, nationalIdCol);
                    string rawPhone    = GetCell(row, phoneCol);
                    string email       = GetCell(row, emailCol);
                    string uniStId     = GetCell(row, uniStudentIdCol);

                    // ── 6a. Required field check ──────────────────────────
                    if (string.IsNullOrWhiteSpace(fullName))
                    {
                        result.Skipped++;
                        result.Errors.Add($"Row {rowNum}: FullName is empty — skipped.");
                        continue;
                    }
                    if (string.IsNullOrWhiteSpace(batchCode))
                    {
                        result.Skipped++;
                        result.Errors.Add($"Row {rowNum}: BatchCode is empty — skipped.");
                        continue;
                    }
                    if (string.IsNullOrWhiteSpace(groupCode))
                    {
                        result.Skipped++;
                        result.Errors.Add($"Row {rowNum}: GroupCode is empty — skipped.");
                        continue;
                    }

                    // ── 6b. Resolve Batch ─────────────────────────────────
                    if (!batchesByCode.TryGetValue(batchCode, out var batch))
                    {
                        result.Skipped++;
                        result.Errors.Add($"Row {rowNum}: Batch '{batchCode}' not found — skipped.");
                        continue;
                    }

                    // ── 6c. Resolve Group ─────────────────────────────────
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

                    // ── 6d. NationalId dedup (optional field) ─────────────
                    if (!string.IsNullOrEmpty(nationalId))
                    {
                        if (existingNationalIds.Contains(nationalId.ToLower()) || batchNationalIds.Contains(nationalId))
                        {
                            result.Skipped++;
                            result.Errors.Add($"Row {rowNum}: NationalId '{nationalId}' already exists — skipped.");
                            continue;
                        }
                    }

                    // ── 6e. UniversityStudentId — auto-generate if missing ─
                    if (string.IsNullOrEmpty(uniStId))
                    {
                        var year = DateTime.UtcNow.Year;
                        do { uniStId = $"STU{year}{autoIdCounter++:D4}"; }
                        while (existingUniStudentIds.Contains(uniStId.ToLower()) || batchUniStudentIds.Contains(uniStId));
                        result.Warnings.Add($"Row {rowNum}: UniversityStudentId auto-generated as '{uniStId}'.");
                    }
                    else if (existingUniStudentIds.Contains(uniStId.ToLower()) || batchUniStudentIds.Contains(uniStId))
                    {
                        result.Skipped++;
                        result.Errors.Add($"Row {rowNum}: UniversityStudentId '{uniStId}' already exists — skipped.");
                        continue;
                    }

                    // ── 6f. Phone normalization ───────────────────────────
                    string phone = "01000000000"; // default placeholder
                    if (!string.IsNullOrEmpty(rawPhone))
                    {
                        // Normalize +20 / 0020 prefix
                        var normalized = rawPhone.Replace(" ", "").Replace("-", "");
                        if (normalized.StartsWith("+20")) normalized = "0" + normalized[3..];
                        else if (normalized.StartsWith("0020")) normalized = "0" + normalized[4..];

                        if (System.Text.RegularExpressions.Regex.IsMatch(normalized, @"^01[0125][0-9]{8}$"))
                            phone = normalized;
                        else
                            result.Warnings.Add($"Row {rowNum}: Phone '{rawPhone}' is invalid — using placeholder '01000000000'.");
                    }

                    // ── 6g. Always generate university email from student ID ──────
                    var department = batch.Department;
                    var college    = department.College;
                    var university = college.University;

                    // University email is always auto-generated — used for login
                    string universityEmail = $"{uniStId.ToLower()}@{_uniSettings.StudentEmailDomain}";
                    // Personal email stored separately on the Student record (optional)
                    string personalEmail = string.IsNullOrWhiteSpace(email) ? "" : email;

                    // ── 6h. Build entities ────────────────────────────────
                    var systemUser = new SystemUser
                    {
                        Id              = Ulid.NewUlid(),
                        FullName        = fullName,
                        Email           = universityEmail,
                        UniversityEmail = universityEmail,
                        NationalId      = nationalId ?? "",
                        PasswordHash    = BCrypt.Net.BCrypt.HashPassword(_uniSettings.DefaultPassword),
                        Role            = UserRole.Student,
                        IsActive        = true,
                        MustChangePassword = true
                    };

                    var student = new Student
                    {
                        Id                  = Ulid.NewUlid(),
                        FullName            = fullName,
                        Email               = personalEmail,
                        UniversityStudentId = uniStId,
                        Phone               = phone,
                        UniversityId        = university.Id,
                        CollegeId           = college.Id,
                        DepartmentId        = department.Id,
                        BatchId             = batch.Id,
                        GroupId             = group.Id,
                        IsActive            = true,
                        RegulationId        = batch.RegulationId,  // inherit regulation from batch
                        SystemUser          = systemUser
                    };

                    newStudents.Add(student);

                    // Track for in-file dedup
                    if (!string.IsNullOrEmpty(nationalId)) batchNationalIds.Add(nationalId);
                    batchUniStudentIds.Add(uniStId);
                    existingUniStudentIds.Add(uniStId.ToLower());
                    if (!string.IsNullOrEmpty(nationalId)) existingNationalIds.Add(nationalId.ToLower());

                    result.Imported++;
                }
                catch (Core.Exceptions.DomainException dex)
                {
                    result.Skipped++;
                    result.Errors.Add($"Row {rowNum}: Validation error — {dex.Message} — skipped.");
                }
                catch (Exception ex)
                {
                    result.Skipped++;
                    result.Errors.Add($"Row {rowNum}: Unexpected error — {ex.Message} — skipped.");
                }
            }

            // ── 7. Single bulk insert ──────────────────────────────────────
            if (newStudents.Count > 0)
            {
                _context.Students.AddRange(newStudents);
                await _context.SaveChangesAsync();
            }

            result.TemporaryPassword = _uniSettings.DefaultPassword;
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

            // Partial strategy: bad rows are skipped, good rows are saved.
            // All-or-nothing was replaced because one bad row should not rollback 49 correct ones.
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
                        existingGrades[studentId] = gradeRecord;
                    }
                    else
                    {
                        // ── IsFinalized guard — never overwrite a finalized grade ──
                        if (gradeRecord.IsFinalized)
                        {
                            result.Skipped++;
                            result.Errors.Add($"Row {rowNum}: Grade for '{uniId}' is already finalized — skipped.");
                            continue;
                        }
                        if (gradeRecord.DeletedAt != null) gradeRecord.DeletedAt = null;
                        _context.Entry(gradeRecord).State = EntityState.Modified;
                    }

                    // Only update fields that are present in the sheet (partial update)
                    if (midterm.HasValue)    gradeRecord.MidtermScore    = midterm.Value;
                    if (coursework.HasValue) gradeRecord.CourseworkScore = coursework.Value;
                    if (final.HasValue)      gradeRecord.FinalExamScore  = final.Value;

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
