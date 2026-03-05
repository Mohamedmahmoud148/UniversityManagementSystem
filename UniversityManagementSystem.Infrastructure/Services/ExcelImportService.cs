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
    }
}
