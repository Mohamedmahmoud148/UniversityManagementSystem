using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using NUlid;
using UniversityManagementSystem.Core.DTOs;
using UniversityManagementSystem.Core.Entities;
using UniversityManagementSystem.Core.Interfaces;
using UniversityManagementSystem.Infrastructure.Data;

namespace UniversityManagementSystem.Infrastructure.Services
{
    public class EnrollmentUploadService(
        AppDbContext context,
        IStorageService storage) : IEnrollmentUploadService
    {
        private readonly AppDbContext _context = context;
        private readonly IStorageService _storage = storage;
        private const string Folder = "enrollment";

        public async Task<EnrollmentUploadResultDto> ProcessExcelAsync(Ulid adminId, IFormFile file)
        {
            var result = new EnrollmentUploadResultDto();

            // 1. Upload raw file to R2 for audit trail
            string storageKey;
            using (var uploadStream = file.OpenReadStream())
            {
                storageKey = await _storage.UploadAsync(uploadStream, file.FileName, file.ContentType, Folder);
            }

            // 2. Create EnrollmentUpload tracking record
            var upload = new EnrollmentUpload
            {
                FileName = file.FileName,
                StorageKey = storageKey,
                UploadedByAdminId = adminId,
                Status = EnrollmentUploadStatus.Processing
            };
            _context.EnrollmentUploads.Add(upload);
            await _context.SaveChangesAsync();
            result.UploadId = upload.Id.ToString();

            try
            {
                // 3. Parse Excel
                using var memStream = new MemoryStream();
                using (var fileStream = file.OpenReadStream())
                    await fileStream.CopyToAsync(memStream);
                memStream.Position = 0;

                using var workbook = new XLWorkbook(memStream);
                var sheet = workbook.Worksheet(1);
                var range = sheet.RangeUsed();

                if (range == null)
                {
                    result.Errors.Add("Worksheet is empty.");
                    await FinishUpload(upload, result, EnrollmentUploadStatus.Failed);
                    return result;
                }

                var dataRows = range.RowsUsed().Skip(1).ToList(); // skip header

                // 4. Pre-fetch lookup dictionaries (zero N+1 hits)
                var deptByCode = await _context.Departments
                    .Include(d => d.College).ThenInclude(c => c.University)
                    .AsNoTracking()
                    .ToDictionaryAsync(d => d.Code, StringComparer.OrdinalIgnoreCase);

                var batchByCode = await _context.Batches
                    .AsNoTracking()
                    .ToDictionaryAsync(b => b.Code, StringComparer.OrdinalIgnoreCase);

                var groupByCode = await _context.Groups
                    .AsNoTracking()
                    .ToDictionaryAsync(g => g.Code, StringComparer.OrdinalIgnoreCase);

                var existingEmails = await _context.Students
                    .AsNoTracking()
                    .Select(s => s.Email.ToLower())
                    .ToHashSetAsync();

                var existingStudentIds = await _context.Students
                    .AsNoTracking()
                    .Select(s => s.UniversityStudentId.ToLower())
                    .ToHashSetAsync();

                var batchEmails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var batchStudentIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                var newStudents = new List<Student>();

                // 5. Process rows
                // Expected columns: 1=StudentName | 2=NationalId | 3=Email | 4=DepartmentCode | 5=BatchCode | 6=GroupCode
                foreach (var row in dataRows)
                {
                    int rowNum = row.RowNumber();
                    try
                    {
                        string fullName = row.Cell(1).GetValue<string>().Trim();
                        string nationalId = row.Cell(2).GetValue<string>().Trim();
                        string email = row.Cell(3).GetValue<string>().Trim();
                        string deptCode = row.Cell(4).GetValue<string>().Trim();
                        string batchCode = row.Cell(5).GetValue<string>().Trim();
                        string groupCode = row.Cell(6).GetValue<string>().Trim();

                        // Required field check
                        if (string.IsNullOrEmpty(fullName) || string.IsNullOrEmpty(email) ||
                            string.IsNullOrEmpty(nationalId) || string.IsNullOrEmpty(deptCode) ||
                            string.IsNullOrEmpty(batchCode) || string.IsNullOrEmpty(groupCode))
                        {
                            result.SkippedCount++;
                            result.Errors.Add($"Row {rowNum}: Missing required fields — skipped.");
                            continue;
                        }

                        // Resolve DepartmentCode → entity
                        if (!deptByCode.TryGetValue(deptCode, out var dept))
                        {
                            result.SkippedCount++;
                            result.Errors.Add($"Row {rowNum}: Department '{deptCode}' not found — skipped.");
                            continue;
                        }

                        // Resolve BatchCode → entity
                        if (!batchByCode.TryGetValue(batchCode, out var batch))
                        {
                            result.SkippedCount++;
                            result.Errors.Add($"Row {rowNum}: Batch '{batchCode}' not found — skipped.");
                            continue;
                        }

                        // Resolve GroupCode → entity
                        if (!groupByCode.TryGetValue(groupCode, out var group))
                        {
                            result.SkippedCount++;
                            result.Errors.Add($"Row {rowNum}: Group '{groupCode}' not found — skipped.");
                            continue;
                        }

                        // Duplicate detection
                        if (existingEmails.Contains(email.ToLower()) || batchEmails.Contains(email))
                        {
                            result.SkippedCount++;
                            result.Errors.Add($"Row {rowNum}: Email '{email}' already exists — skipped.");
                            continue;
                        }

                        if (existingStudentIds.Contains(nationalId.ToLower()) || batchStudentIds.Contains(nationalId))
                        {
                            result.SkippedCount++;
                            result.Errors.Add($"Row {rowNum}: National ID '{nationalId}' already exists — skipped.");
                            continue;
                        }

                        // Build SystemUser + Student
                        var systemUser = new SystemUser
                        {
                            Id = Ulid.NewUlid(),
                            FullName = fullName,
                            NationalId = nationalId,
                            Email = email,
                            UniversityEmail = $"{nationalId.ToLower()}@university.edu",
                            PasswordHash = BCrypt.Net.BCrypt.HashPassword("TempPass@123"),
                            Role = UserRole.Student,
                            IsActive = true
                        };

                        var student = new Student
                        {
                            Id = Ulid.NewUlid(),
                            FullName = fullName,
                            Email = email,
                            UniversityStudentId = nationalId,
                            Phone = "01000000000", // placeholder — update post-import
                            UniversityId = dept.College.UniversityId,
                            CollegeId = dept.CollegeId,
                            DepartmentId = dept.Id,
                            BatchId = batch.Id,
                            GroupId = group.Id,
                            IsActive = true,
                            SystemUser = systemUser
                        };

                        newStudents.Add(student);
                        batchEmails.Add(email);
                        batchStudentIds.Add(nationalId);
                        result.CreatedCount++;
                    }
                    catch (Exception ex)
                    {
                        result.SkippedCount++;
                        result.Errors.Add($"Row {rowNum}: Unexpected error — {ex.Message} — skipped.");
                    }
                }

                // 6. Bulk insert
                if (newStudents.Count > 0)
                {
                    _context.Students.AddRange(newStudents);
                    await _context.SaveChangesAsync();
                }

                result.Success = true;
                await FinishUpload(upload, result, EnrollmentUploadStatus.Completed);
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Errors.Add($"Fatal error: {ex.Message}");
                await FinishUpload(upload, result, EnrollmentUploadStatus.Failed);
            }

            return result;
        }

        private async Task FinishUpload(EnrollmentUpload upload, EnrollmentUploadResultDto result, EnrollmentUploadStatus status)
        {
            upload.Status = status;
            upload.CreatedCount = result.CreatedCount;
            upload.SkippedCount = result.SkippedCount;
            upload.Errors = result.Errors.Count > 0 ? string.Join("\n", result.Errors) : null;
            _context.Entry(upload).State = Microsoft.EntityFrameworkCore.EntityState.Modified;
            await _context.SaveChangesAsync();
        }
    }
}
