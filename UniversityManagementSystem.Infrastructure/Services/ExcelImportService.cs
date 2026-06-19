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
    public class ExcelImportService(AppDbContext context, IIdentityProvisioningService provisioningService,
        IOptions<UniversitySettings> uniOptions, IUniversityEmailGenerator emailGenerator) : IExcelImportService
    {
        private readonly AppDbContext _context = context;
        private readonly IIdentityProvisioningService _provisioningService = provisioningService;
        private readonly UniversitySettings _uniSettings = uniOptions.Value;
        private readonly IUniversityEmailGenerator _emailGenerator = emailGenerator;

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
                    string universityEmail = await _emailGenerator.GenerateStudentEmailAsync(fullName);

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

            // ── Required columns ───────────────────────────────────────────
            int fullNameCol     = FindCol("fullname", "name", "studentname", "الاسم", "اسم");
            int nationalIdCol   = FindCol("nationalid", "national", "رقمقومي", "الرقمالقومي");
            int phoneCol        = FindCol("phone", "mobile", "tel", "هاتف", "موبايل", "تليفون");
            int batchCodeCol    = FindCol("batchcode", "batch", "دفعة", "الدفعة", "كود الدفعة");
            int groupCodeCol    = FindCol("groupcode", "group", "مجموعة", "المجموعة");

            // ── Optional columns ───────────────────────────────────────────
            int emailCol          = FindCol("email", "personalmail", "mail", "ايميل");
            int uniStudentIdCol   = FindCol("universitystudentid", "studentid", "studentnumber", "رقمطالب", "كودطالب");
            int collegeCodeCol    = FindCol("collegecode", "college", "كلية", "الكلية");
            int deptCodeCol       = FindCol("departmentcode", "department", "dept", "قسم", "القسم");
            int governorateCol    = FindCol("governorate", "gov", "محافظة", "المحافظة");
            int addressCol        = FindCol("address", "عنوان", "العنوان");
            int genderCol         = FindCol("gender", "sex", "النوع", "الجنس", "نوع");
            int dobCol            = FindCol("dateofbirth", "dob", "birthdate", "تاريخالميلاد", "ميلاد");
            int studentTypeCol    = FindCol("studenttype", "type", "نوعالطالب", "نوع");
            int religionCol       = FindCol("religion", "الديانة", "ديانة");

            // ── 4. Required column check ───────────────────────────────────
            var missing = new List<string>();
            if (fullNameCol  == -1) missing.Add("FullName (الاسم)");
            if (nationalIdCol == -1) missing.Add("NationalId (رقم قومي)");
            if (phoneCol     == -1) missing.Add("Phone (تليفون)");
            if (batchCodeCol == -1) missing.Add("BatchCode (كود الدفعة)");
            if (groupCodeCol == -1) missing.Add("GroupCode (المجموعة)");

            if (missing.Count > 0)
                return Fail(result, $"Required columns not found: {string.Join(", ", missing)}. " +
                    "Make sure the first row contains headers.");

            // Warn about missing optional columns
            if (emailCol        == -1) result.Warnings.Add("Column 'Email' not found — university email will be auto-generated.");
            if (uniStudentIdCol == -1) result.Warnings.Add("Column 'UniversityStudentId' not found — will be auto-generated.");
            if (governorateCol  == -1) result.Warnings.Add("Column 'Governorate' not found — will be left empty.");
            if (addressCol      == -1) result.Warnings.Add("Column 'Address' not found — will be left empty.");
            if (genderCol       == -1) result.Warnings.Add("Column 'Gender' not found — will be left empty.");
            if (dobCol          == -1) result.Warnings.Add("Column 'DateOfBirth' not found — will be left empty.");
            if (studentTypeCol  == -1) result.Warnings.Add("Column 'StudentType' not found — will default to Regular.");
            if (religionCol     == -1) result.Warnings.Add("Column 'Religion' not found — will be left empty.");

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
                    string nationalId  = GetCell(row, nationalIdCol);
                    string rawPhone    = GetCell(row, phoneCol);
                    string batchCode   = GetCell(row, batchCodeCol);
                    string groupCode   = GetCell(row, groupCodeCol);
                    string email       = GetCell(row, emailCol);
                    string uniStId     = GetCell(row, uniStudentIdCol);
                    string governorate = GetCell(row, governorateCol);
                    string address     = GetCell(row, addressCol);
                    string genderRaw   = GetCell(row, genderCol);
                    string dobRaw      = GetCell(row, dobCol);
                    string typeRaw     = GetCell(row, studentTypeCol);
                    string religion    = GetCell(row, religionCol);

                    // ── 6a. Required field check ──────────────────────────
                    var rowErrors = new List<string>();
                    if (string.IsNullOrWhiteSpace(fullName))    rowErrors.Add("FullName (الاسم) is empty");
                    if (string.IsNullOrWhiteSpace(nationalId))  rowErrors.Add("NationalId (رقم قومي) is empty");
                    if (string.IsNullOrWhiteSpace(rawPhone))    rowErrors.Add("Phone (تليفون) is empty");
                    if (string.IsNullOrWhiteSpace(batchCode))   rowErrors.Add("BatchCode (كود الدفعة) is empty");
                    if (string.IsNullOrWhiteSpace(groupCode))   rowErrors.Add("GroupCode (المجموعة) is empty");
                    if (rowErrors.Count > 0)
                    {
                        var errMsg = string.Join("; ", rowErrors);
                        result.Skipped++;
                        result.Errors.Add($"Row {rowNum}: {errMsg} — skipped.");
                        result.FailedRows.Add(new Core.DTOs.FailedImportRow { RowNumber = rowNum, FullName = fullName, NationalId = nationalId, BatchCode = batchCode, GroupCode = groupCode, ErrorMessage = errMsg });
                        continue;
                    }

                    // ── 6b. NationalId format (14 digits) ────────────────
                    if (!System.Text.RegularExpressions.Regex.IsMatch(nationalId, @"^\d{14}$"))
                    {
                        var errMsg = $"NationalId '{nationalId}' must be exactly 14 digits";
                        result.Skipped++;
                        result.Errors.Add($"Row {rowNum}: {errMsg} — skipped.");
                        result.FailedRows.Add(new Core.DTOs.FailedImportRow { RowNumber = rowNum, FullName = fullName, NationalId = nationalId, BatchCode = batchCode, GroupCode = groupCode, ErrorMessage = errMsg });
                        continue;
                    }
                    if (existingNationalIds.Contains(nationalId.ToLower()) || batchNationalIds.Contains(nationalId))
                    {
                        var errMsg = $"NationalId '{nationalId}' already exists (duplicate)";
                        result.Skipped++;
                        result.Errors.Add($"Row {rowNum}: {errMsg} — skipped.");
                        result.FailedRows.Add(new Core.DTOs.FailedImportRow { RowNumber = rowNum, FullName = fullName, NationalId = nationalId, BatchCode = batchCode, GroupCode = groupCode, ErrorMessage = errMsg });
                        continue;
                    }

                    // ── 6c. Resolve Batch ─────────────────────────────────
                    if (!batchesByCode.TryGetValue(batchCode, out var batch))
                    {
                        var errMsg = $"Batch '{batchCode}' not found in system";
                        result.Skipped++;
                        result.Errors.Add($"Row {rowNum}: {errMsg} — skipped.");
                        result.FailedRows.Add(new Core.DTOs.FailedImportRow { RowNumber = rowNum, FullName = fullName, NationalId = nationalId, BatchCode = batchCode, GroupCode = groupCode, ErrorMessage = errMsg });
                        continue;
                    }

                    // ── 6d. Validate CollegeCode / DeptCode if provided ───
                    string collegeCode = GetCell(row, collegeCodeCol);
                    string deptCode    = GetCell(row, deptCodeCol);
                    if (!string.IsNullOrWhiteSpace(collegeCode) &&
                        !string.Equals(batch.Department.College.Code, collegeCode, StringComparison.OrdinalIgnoreCase))
                    {
                        result.Warnings.Add($"Row {rowNum}: CollegeCode '{collegeCode}' doesn't match batch college '{batch.Department.College.Code}' — batch college used.");
                    }
                    if (!string.IsNullOrWhiteSpace(deptCode) &&
                        !string.Equals(batch.Department.Code, deptCode, StringComparison.OrdinalIgnoreCase))
                    {
                        result.Warnings.Add($"Row {rowNum}: DepartmentCode '{deptCode}' doesn't match batch department '{batch.Department.Code}' — batch department used.");
                    }

                    // ── 6e. Resolve Group ─────────────────────────────────
                    if (!groupsByCode.TryGetValue(groupCode, out var group))
                    {
                        var errMsg = $"Group '{groupCode}' not found in system";
                        result.Skipped++;
                        result.Errors.Add($"Row {rowNum}: {errMsg} — skipped.");
                        result.FailedRows.Add(new Core.DTOs.FailedImportRow { RowNumber = rowNum, FullName = fullName, NationalId = nationalId, BatchCode = batchCode, GroupCode = groupCode, ErrorMessage = errMsg });
                        continue;
                    }
                    if (group.BatchId != batch.Id)
                    {
                        var errMsg = $"Group '{groupCode}' does not belong to batch '{batchCode}'";
                        result.Skipped++;
                        result.Errors.Add($"Row {rowNum}: {errMsg} — skipped.");
                        result.FailedRows.Add(new Core.DTOs.FailedImportRow { RowNumber = rowNum, FullName = fullName, NationalId = nationalId, BatchCode = batchCode, GroupCode = groupCode, ErrorMessage = errMsg });
                        continue;
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
                        var errMsg = $"UniversityStudentId '{uniStId}' already exists (duplicate)";
                        result.Skipped++;
                        result.Errors.Add($"Row {rowNum}: {errMsg} — skipped.");
                        result.FailedRows.Add(new Core.DTOs.FailedImportRow { RowNumber = rowNum, FullName = fullName, NationalId = nationalId, BatchCode = batchCode, GroupCode = groupCode, ErrorMessage = errMsg });
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

                    // ── 6g. Parse optional profile fields ────────────────
                    Core.Entities.Gender? gender = null;
                    if (!string.IsNullOrWhiteSpace(genderRaw))
                    {
                        var g = genderRaw.Trim().ToLower();
                        if (g is "male" or "m" or "ذكر" or "1") gender = Core.Entities.Gender.Male;
                        else if (g is "female" or "f" or "أنثى" or "انثى" or "2") gender = Core.Entities.Gender.Female;
                        else result.Warnings.Add($"Row {rowNum}: Gender '{genderRaw}' not recognized — left empty.");
                    }

                    DateTime? dateOfBirth = null;
                    if (!string.IsNullOrWhiteSpace(dobRaw))
                    {
                        if (DateTime.TryParse(dobRaw, out var dob)) dateOfBirth = DateTime.SpecifyKind(dob, DateTimeKind.Utc);
                        else result.Warnings.Add($"Row {rowNum}: DateOfBirth '{dobRaw}' is invalid — left empty.");
                    }

                    var studentType = Core.Entities.StudentType.Regular;
                    if (!string.IsNullOrWhiteSpace(typeRaw))
                    {
                        var t = typeRaw.Trim().ToLower();
                        if (t is "transfer" or "منقول")         studentType = Core.Entities.StudentType.Transfer;
                        else if (t is "repeating" or "معيد")    studentType = Core.Entities.StudentType.Repeating;
                        else if (t is "external" or "انتساب")   studentType = Core.Entities.StudentType.External;
                        else if (t is not "regular" or "منتظم") result.Warnings.Add($"Row {rowNum}: StudentType '{typeRaw}' not recognized — defaulting to Regular.");
                    }

                    // ── 6h. Auto-generate university email (new format: name.studentN@benisuefnationaluniversity.edu) ──
                    var department = batch.Department;
                    var college    = department.College;
                    var university = college.University;

                    string universityEmail = await _emailGenerator.GenerateStudentEmailAsync(fullName);
                    string personalEmail   = string.IsNullOrWhiteSpace(email) ? "" : email;

                    // ── 6i. Build entities ────────────────────────────────
                    var systemUser = new SystemUser
                    {
                        Id                 = Ulid.NewUlid(),
                        FullName           = fullName,
                        Email              = universityEmail,
                        UniversityEmail    = universityEmail,
                        NationalId         = nationalId,
                        PasswordHash       = BCrypt.Net.BCrypt.HashPassword(_uniSettings.DefaultPassword),
                        Role               = UserRole.Student,
                        IsActive           = true,
                        MustChangePassword = true
                    };

                    var student = new Student
                    {
                        Id                  = Ulid.NewUlid(),
                        FullName            = fullName,
                        NationalId          = nationalId,
                        Email               = personalEmail,
                        Phone               = phone,
                        Governorate         = governorate,
                        Address             = address,
                        Gender              = gender,
                        DateOfBirth         = dateOfBirth,
                        StudentType         = studentType,
                        Religion            = religion,
                        UniversityStudentId = uniStId,
                        UniversityId        = university.Id,
                        CollegeId           = college.Id,
                        DepartmentId        = department.Id,
                        BatchId             = batch.Id,
                        GroupId             = group.Id,
                        IsActive            = true,
                        RegulationId        = batch.RegulationId,
                        SystemUser          = systemUser
                    };

                    newStudents.Add(student);

                    // Collect credentials for the download Excel
                    result.ImportedCredentials.Add(new Core.DTOs.StudentCredentialRow
                    {
                        FullName            = fullName,
                        UniversityStudentId = uniStId,
                        UniversityEmail     = universityEmail,
                        TemporaryPassword   = _uniSettings.DefaultPassword,
                        BatchCode           = batchCode,
                        GroupCode           = groupCode,
                        Department          = department.Name
                    });

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
                    result.FailedRows.Add(new Core.DTOs.FailedImportRow { RowNumber = rowNum, ErrorMessage = $"Validation: {dex.Message}" });
                }
                catch (Exception ex)
                {
                    result.Skipped++;
                    result.Errors.Add($"Row {rowNum}: Unexpected error — {ex.Message} — skipped.");
                    result.FailedRows.Add(new Core.DTOs.FailedImportRow { RowNumber = rowNum, ErrorMessage = $"Unexpected: {ex.Message}" });
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

        // ── Generate credentials Excel ────────────────────────────────────────
        public Task<byte[]> GenerateCredentialsExcelAsync(
            IReadOnlyList<Core.DTOs.StudentCredentialRow> credentials,
            string universityName)
        {
            using var workbook  = new XLWorkbook();
            var ws = workbook.Worksheets.Add("Student Credentials");

            // ── Title row ────────────────────────────────────────────────────
            ws.Cell(1, 1).Value = $"{universityName} — Student Login Credentials";
            ws.Range(1, 1, 1, 7).Merge();
            var titleCell = ws.Cell(1, 1);
            titleCell.Style.Font.Bold      = true;
            titleCell.Style.Font.FontSize  = 14;
            titleCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            titleCell.Style.Fill.BackgroundColor = XLColor.FromHtml("#1E3A5F");
            titleCell.Style.Font.FontColor = XLColor.White;

            // ── Sub-title ────────────────────────────────────────────────────
            ws.Cell(2, 1).Value = $"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC  •  Total Students: {credentials.Count}  •  ⚠ Share securely — contains passwords";
            ws.Range(2, 1, 2, 7).Merge();
            var subCell = ws.Cell(2, 1);
            subCell.Style.Font.Italic    = true;
            subCell.Style.Font.FontSize  = 9;
            subCell.Style.Font.FontColor = XLColor.DarkRed;
            subCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            // ── Header row (row 3) ────────────────────────────────────────────
            var headers = new[] { "#", "Full Name", "University ID", "University Email", "Temporary Password", "Batch", "Department" };
            for (int c = 0; c < headers.Length; c++)
            {
                var cell = ws.Cell(3, c + 1);
                cell.Value = headers[c];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#2E75B6");
                cell.Style.Font.FontColor = XLColor.White;
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                cell.Style.Border.BottomBorder = XLBorderStyleValues.Medium;
            }

            // ── Data rows ─────────────────────────────────────────────────────
            for (int i = 0; i < credentials.Count; i++)
            {
                var row = credentials[i];
                int r   = i + 4;
                bool isEven = i % 2 == 0;

                ws.Cell(r, 1).Value = i + 1;
                ws.Cell(r, 2).Value = row.FullName;
                ws.Cell(r, 3).Value = row.UniversityStudentId;
                ws.Cell(r, 4).Value = row.UniversityEmail;
                ws.Cell(r, 5).Value = row.TemporaryPassword;
                ws.Cell(r, 6).Value = row.BatchCode;
                ws.Cell(r, 7).Value = row.Department;

                // Alternate row shading
                if (isEven)
                {
                    ws.Range(r, 1, r, 7).Style.Fill.BackgroundColor = XLColor.FromHtml("#EBF3FB");
                }

                // Highlight password cell
                ws.Cell(r, 5).Style.Font.Bold = true;
                ws.Cell(r, 5).Style.Font.FontColor = XLColor.DarkRed;
            }

            // ── Note row at the bottom ────────────────────────────────────────
            int noteRow = credentials.Count + 5;
            ws.Cell(noteRow, 1).Value = "⚠  Students must change their password on first login.  This file contains sensitive data — do not share publicly.";
            ws.Range(noteRow, 1, noteRow, 7).Merge();
            ws.Cell(noteRow, 1).Style.Font.Italic    = true;
            ws.Cell(noteRow, 1).Style.Font.FontColor = XLColor.DarkRed;
            ws.Cell(noteRow, 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#FFF2CC");

            // ── Column widths ─────────────────────────────────────────────────
            ws.Column(1).Width = 5;
            ws.Column(2).Width = 28;
            ws.Column(3).Width = 18;
            ws.Column(4).Width = 36;
            ws.Column(5).Width = 20;
            ws.Column(6).Width = 14;
            ws.Column(7).Width = 22;

            // Freeze header rows
            ws.SheetView.Freeze(3, 0);

            using var ms = new MemoryStream();
            workbook.SaveAs(ms);
            return Task.FromResult(ms.ToArray());
        }

        // ── Generate full import-result report (3 sheets) ────────────────────
        public Task<byte[]> GenerateImportReportAsync(ImportStudentsResultDto result, string universityName)
        {
            using var workbook = new XLWorkbook();
            var generated = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm") + " UTC";

            // ── SHEET 1: Successful Students ──────────────────────────────────
            var ws1 = workbook.Worksheets.Add("✓ Successful Students");
            ws1.Cell(1, 1).Value = $"{universityName} — Import Report: Successful Students";
            ws1.Range(1, 1, 1, 9).Merge();
            StyleTitle(ws1.Cell(1, 1), "#1E6B3E");

            ws1.Cell(2, 1).Value = $"Generated: {generated}  •  Imported: {result.Imported}  •  Password forced-change on first login";
            ws1.Range(2, 1, 2, 9).Merge();
            StyleSubTitle(ws1.Cell(2, 1));

            var h1 = new[] { "#", "Full Name", "University ID", "Academic Email", "Temp Password", "Batch", "Group", "Department", "Status" };
            for (int c = 0; c < h1.Length; c++)
            {
                StyleHeader(ws1.Cell(3, c + 1), "#2E75B6", h1[c]);
            }

            for (int i = 0; i < result.ImportedCredentials.Count; i++)
            {
                var cr = result.ImportedCredentials[i];
                int r = i + 4;
                bool isEven = i % 2 == 0;
                ws1.Cell(r, 1).Value = i + 1;
                ws1.Cell(r, 2).Value = cr.FullName;
                ws1.Cell(r, 3).Value = cr.UniversityStudentId;
                ws1.Cell(r, 4).Value = cr.UniversityEmail;
                ws1.Cell(r, 5).Value = cr.TemporaryPassword;
                ws1.Cell(r, 6).Value = cr.BatchCode;
                ws1.Cell(r, 7).Value = cr.GroupCode;
                ws1.Cell(r, 8).Value = cr.Department;
                ws1.Cell(r, 9).Value = "✓ Imported";
                if (isEven) ws1.Range(r, 1, r, 9).Style.Fill.BackgroundColor = XLColor.FromHtml("#EBF3FB");
                ws1.Cell(r, 5).Style.Font.Bold = true;
                ws1.Cell(r, 5).Style.Font.FontColor = XLColor.DarkRed;
                ws1.Cell(r, 9).Style.Font.FontColor = XLColor.FromHtml("#1E6B3E");
            }

            int[] w1 = [5, 28, 18, 36, 20, 14, 12, 22, 14];
            for (int i = 0; i < w1.Length; i++) ws1.Column(i + 1).Width = w1[i];
            ws1.SheetView.Freeze(3, 0);

            // ── SHEET 2: Failed / Skipped Rows ────────────────────────────────
            var ws2 = workbook.Worksheets.Add("✗ Failed Rows");
            ws2.Cell(1, 1).Value = $"{universityName} — Import Report: Failed / Skipped Rows";
            ws2.Range(1, 1, 1, 7).Merge();
            StyleTitle(ws2.Cell(1, 1), "#C00000");

            ws2.Cell(2, 1).Value = $"Generated: {generated}  •  Failed: {result.Skipped}  •  Fix errors and re-import these rows";
            ws2.Range(2, 1, 2, 7).Merge();
            StyleSubTitle(ws2.Cell(2, 1));

            var h2 = new[] { "Row #", "Full Name", "National ID", "Batch Code", "Group Code", "Status", "Error Message" };
            for (int c = 0; c < h2.Length; c++)
                StyleHeader(ws2.Cell(3, c + 1), "#C00000", h2[c]);

            for (int i = 0; i < result.FailedRows.Count; i++)
            {
                var fr = result.FailedRows[i];
                int r = i + 4;
                bool isEven = i % 2 == 0;
                ws2.Cell(r, 1).Value = fr.RowNumber;
                ws2.Cell(r, 2).Value = fr.FullName;
                ws2.Cell(r, 3).Value = fr.NationalId;
                ws2.Cell(r, 4).Value = fr.BatchCode;
                ws2.Cell(r, 5).Value = fr.GroupCode;
                ws2.Cell(r, 6).Value = "✗ Failed";
                ws2.Cell(r, 7).Value = fr.ErrorMessage;
                if (isEven) ws2.Range(r, 1, r, 7).Style.Fill.BackgroundColor = XLColor.FromHtml("#FFF0F0");
                ws2.Cell(r, 6).Style.Font.FontColor = XLColor.DarkRed;
                ws2.Cell(r, 7).Style.Font.FontColor = XLColor.DarkRed;
            }

            if (result.FailedRows.Count == 0)
            {
                ws2.Cell(4, 1).Value = "No failed rows — all records were imported successfully.";
                ws2.Range(4, 1, 4, 7).Merge();
                ws2.Cell(4, 1).Style.Font.Italic = true;
                ws2.Cell(4, 1).Style.Font.FontColor = XLColor.FromHtml("#1E6B3E");
            }

            int[] w2 = [8, 28, 18, 14, 12, 12, 55];
            for (int i = 0; i < w2.Length; i++) ws2.Column(i + 1).Width = w2[i];
            ws2.SheetView.Freeze(3, 0);

            // ── SHEET 3: Summary ──────────────────────────────────────────────
            var ws3 = workbook.Worksheets.Add("📊 Summary");
            ws3.Cell(1, 1).Value = $"{universityName} — Import Summary";
            ws3.Range(1, 1, 1, 3).Merge();
            StyleTitle(ws3.Cell(1, 1), "#1E3A5F");

            ws3.Cell(2, 1).Value = $"Generated: {generated}";
            ws3.Range(2, 1, 2, 3).Merge();
            StyleSubTitle(ws3.Cell(2, 1));

            var stats = new (string Label, object Value, string? Color)[]
            {
                ("Total Rows in File",    result.TotalRows,        null),
                ("✓ Successfully Imported", result.Imported,       "#1E6B3E"),
                ("✗ Failed / Skipped",   result.Skipped,          "#C00000"),
                ("⚠ Warnings",           result.Warnings.Count,   "#8B6914"),
                ("Validation Errors",    result.Errors.Count,      "#C00000"),
                ("Default Password",     result.TemporaryPassword, "#8B6914"),
            };

            for (int i = 0; i < stats.Length; i++)
            {
                int r = i + 4;
                ws3.Cell(r, 1).Value = stats[i].Label;
                ws3.Cell(r, 1).Style.Font.Bold = true;
                ws3.Cell(r, 2).Value = stats[i].Value?.ToString() ?? "";
                if (stats[i].Color != null)
                    ws3.Cell(r, 2).Style.Font.FontColor = XLColor.FromHtml(stats[i].Color!);
            }

            // Warnings list
            if (result.Warnings.Count > 0)
            {
                int warnStart = stats.Length + 6;
                ws3.Cell(warnStart, 1).Value = "Warnings:";
                ws3.Cell(warnStart, 1).Style.Font.Bold = true;
                ws3.Cell(warnStart, 1).Style.Font.FontColor = XLColor.FromHtml("#8B6914");
                for (int i = 0; i < result.Warnings.Count; i++)
                {
                    ws3.Cell(warnStart + 1 + i, 1).Value = $"• {result.Warnings[i]}";
                    ws3.Range(warnStart + 1 + i, 1, warnStart + 1 + i, 3).Merge();
                    ws3.Cell(warnStart + 1 + i, 1).Style.Font.FontColor = XLColor.FromHtml("#8B6914");
                }
            }

            ws3.Column(1).Width = 30;
            ws3.Column(2).Width = 35;
            ws3.Column(3).Width = 10;

            // ── Note ─────────────────────────────────────────────────────────
            int noteRow2 = stats.Length + 6 + result.Warnings.Count + 3;
            ws3.Cell(noteRow2, 1).Value = "⚠  This report contains sensitive credentials. Do not share publicly.";
            ws3.Range(noteRow2, 1, noteRow2, 3).Merge();
            ws3.Cell(noteRow2, 1).Style.Font.Italic = true;
            ws3.Cell(noteRow2, 1).Style.Font.FontColor = XLColor.DarkRed;
            ws3.Cell(noteRow2, 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#FFF2CC");

            using var ms = new MemoryStream();
            workbook.SaveAs(ms);
            return Task.FromResult(ms.ToArray());
        }

        // ── Shared style helpers ──────────────────────────────────────────────
        private static void StyleTitle(IXLCell cell, string bgHex)
        {
            cell.Style.Font.Bold = true;
            cell.Style.Font.FontSize = 13;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml(bgHex);
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }

        private static void StyleSubTitle(IXLCell cell)
        {
            cell.Style.Font.Italic = true;
            cell.Style.Font.FontSize = 9;
            cell.Style.Font.FontColor = XLColor.DarkRed;
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }

        private static void StyleHeader(IXLCell cell, string bgHex, string value)
        {
            cell.Value = value;
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml(bgHex);
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            cell.Style.Border.BottomBorder = XLBorderStyleValues.Medium;
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
            // NOTE: Order matters — check specific columns first so "midterm" (which contains "id")
            // never collides with the student-ID detection branch.
            int idCol = -1, midtermCol = -1, courseworkCol = -1, finalCol = -1;
            foreach (var cell in headerRow.Cells())
            {
                var val = cell.GetValue<string>().ToLower().Trim();
                if (val.Contains("midterm"))                            midtermCol    = cell.Address.ColumnNumber;
                else if (val.Contains("coursework") || val.Contains("work")) courseworkCol = cell.Address.ColumnNumber;
                else if (val.Contains("final"))                         finalCol      = cell.Address.ColumnNumber;
                else if (val.Contains("student") || val == "id" || val.EndsWith("id")) idCol = cell.Address.ColumnNumber;
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
