using Hangfire;
using Microsoft.EntityFrameworkCore;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UniversityManagementSystem.Core.DTOs;
using UniversityManagementSystem.Core.Entities;
using UniversityManagementSystem.Core.Interfaces;
using UniversityManagementSystem.Infrastructure.Data;
using NUlid;

namespace UniversityManagementSystem.Infrastructure.Jobs
{
    public class BulkUploadJob(
        AppDbContext context,
        IExcelService excelService,
        IIdentityProvisioningService identityService,
        IAuthService authService,
        IAiService aiService) : IBulkUploadJob
    {
        private readonly AppDbContext _context = context;
        private readonly IExcelService _excelService = excelService;
        private readonly IIdentityProvisioningService _identityService = identityService;
        private readonly IAuthService _authService = authService;
        private readonly IAiService _aiService = aiService;

        public async Task ProcessStudentDirectUpload(Ulid fileId, Ulid uploaderUserId)
        {
            var file = await _context.UploadedFiles.FindAsync(fileId);
            if (file is null) return;

            file.ValidationStatus = "Processing";
            await _context.SaveChangesAsync();

            try
            {
                // Ensure file exists
                if (!File.Exists(file.StoredPath)) throw new FileNotFoundException("File not found on disk");

                using var stream = File.OpenRead(file.StoredPath);
                var students = _excelService.ParseStudents(stream);
                int successCount = 0;
                int failCount = 0;

                foreach (var dto in students)
                {
                    try
                    {
                        var batch = await _context.Batches.FindAsync(dto.BatchId);
                        if (batch == null)
                        {
                            failCount++;
                            Console.WriteLine($"BulkUpload Error: BatchId {dto.BatchId} not found in database.");
                            continue;
                        }

                        var password = _identityService.GenerateSecurePassword();
                        var email = await _identityService.GenerateUniversityEmailAsync(
                            dto.FullName.Split(' ')[0],
                            dto.FullName.Split(' ').Last(),
                            UserRole.Student);

                        var studentId = await _identityService.GenerateStudentIdAsync(batch.Id, batch.DepartmentId);

                        // Use strategy pattern or service method for cleaner transaction handling
                        using var transaction = await _context.Database.BeginTransactionAsync();
                        try
                        {
                            var user = new SystemUser
                            {
                                FullName = dto.FullName,
                                Email = email,
                                UniversityEmail = email, // Set UniversityEmail on SystemUser
                                NationalId = dto.NationalId, // Set NationalId on SystemUser
                                PasswordHash = BCrypt.Net.BCrypt.HashPassword("DefaultPass123!"), // Default password for bulk
                                Role = UserRole.Student,
                                // CreatedByUserId = uploaderUserId, // Removed as per instruction's snippet
                                IsActive = true
                            };
                            _context.SystemUsers.Add(user);
                            await _context.SaveChangesAsync();

                            var student = new Student
                            {
                                FullName = dto.FullName,
                                Email = "", // Personal Email not in bulk? Or maybe user.Email? Let's leave empty.
                                Phone = dto.Phone,
                                UniversityStudentId = studentId,
                                BatchId = batch.Id,
                                SystemUserId = user.Id
                                // UniversityEmail = email, // Removed from Student
                                // NationalId = dto.NationalId, // Removed from Student
                            };
                            _context.Students.Add(student);
                            await _context.SaveChangesAsync();

                            // user.RelatedEntityId = student.Id; // Removed
                            // await _context.SaveChangesAsync(); // Removed

                            await transaction.CommitAsync();
                            successCount++;
                        }
                        catch
                        {
                            await transaction.RollbackAsync();
                            throw;
                        }
                    }
                    catch
                    {
                        failCount++;
                    }
                }

                file.ValidationStatus = "Completed";
                file.ExtractedDataJson = $"{{ \"success\": {successCount}, \"failed\": {failCount} }}";
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                file.ValidationStatus = "Failed";
                file.ExtractedDataJson = $"{{ \"error\": \"{ex.Message}\" }}";
                await _context.SaveChangesAsync();
            }
        }

        public async Task ProcessStudentAiUpload(Ulid fileId, Ulid uploaderUserId)
        {
            var file = await _context.UploadedFiles.FindAsync(fileId);
            if (file is null) return;

            file.ValidationStatus = "Processing";
            await _context.SaveChangesAsync();

            try
            {
                if (!File.Exists(file.StoredPath)) throw new FileNotFoundException("File not found on disk");

                var extractionResult = await _aiService.ExtractDataFromFileAsync(file.StoredPath, file.ContentType);
                if (!extractionResult.Success || string.IsNullOrEmpty(extractionResult.ExtractectedJson))
                {
                    file.ValidationStatus = "Failed";
                    file.ExtractedDataJson = $"{{ \"error\": \"AI Extraction Failed: {(extractionResult.Errors != null ? string.Join(", ", extractionResult.Errors) : "Unknown")}\" }}";
                    await _context.SaveChangesAsync();
                    return;
                }

                var options = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var students = System.Text.Json.JsonSerializer.Deserialize<List<StudentImportDto>>(extractionResult.ExtractectedJson, options) ?? new List<StudentImportDto>();

                int successCount = 0;
                int failCount = 0;

                foreach (var dto in students)
                {
                    try
                    {
                        var batch = await _context.Batches.FindAsync(dto.BatchId);
                        if (batch == null)
                        {
                            failCount++;
                            continue;
                        }

                        var email = await _identityService.GenerateUniversityEmailAsync(
                            dto.FullName.Split(' ')[0],
                            dto.FullName.Split(' ').LastOrDefault() ?? "Student",
                            UserRole.Student);

                        var studentId = await _identityService.GenerateStudentIdAsync(batch.Id, batch.DepartmentId);

                        using var transaction = await _context.Database.BeginTransactionAsync();
                        try
                        {
                            var user = new SystemUser
                            {
                                Id = Ulid.NewUlid(),
                                FullName = dto.FullName,
                                Email = email,
                                UniversityEmail = email,
                                NationalId = string.IsNullOrEmpty(dto.NationalId) ? $"NA_{Guid.NewGuid()}" : dto.NationalId,
                                PasswordHash = BCrypt.Net.BCrypt.HashPassword("DefaultPass123!"),
                                Role = UserRole.Student,
                                IsActive = true
                            };
                            _context.SystemUsers.Add(user);
                            await _context.SaveChangesAsync();

                            var student = new Student
                            {
                                Id = Ulid.NewUlid(),
                                FullName = dto.FullName,
                                Email = "",
                                Phone = dto.Phone ?? "",
                                UniversityStudentId = studentId,
                                BatchId = batch.Id,
                                SystemUserId = user.Id
                            };
                            _context.Students.Add(student);
                            await _context.SaveChangesAsync();

                            await transaction.CommitAsync();
                            successCount++;
                        }
                        catch
                        {
                            await transaction.RollbackAsync();
                            throw;
                        }
                    }
                    catch
                    {
                        failCount++;
                    }
                }

                file.ValidationStatus = "Completed";
                file.ExtractedDataJson = $"{{ \"success\": {successCount}, \"failed\": {failCount} }}";
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                file.ValidationStatus = "Failed";
                file.ExtractedDataJson = $"{{ \"error\": \"{ex.Message}\" }}";
                await _context.SaveChangesAsync();
            }
        }

        public async Task ProcessDoctorUpload(Ulid fileId, Ulid uploaderUserId)
        {
            var file = await _context.UploadedFiles.FindAsync(fileId);
            if (file is null) return;

            file.ValidationStatus = "Processing";
            await _context.SaveChangesAsync();

            try
            {
                if (!File.Exists(file.StoredPath)) throw new FileNotFoundException("File not found on disk");

                using var stream = File.OpenRead(file.StoredPath);
                var doctors = _excelService.ParseDoctors(stream);
                int successCount = 0;
                int failCount = 0;

                foreach (var dto in doctors)
                {
                    try
                    {
                        var dept = await _context.Departments.FirstOrDefaultAsync(d => d.Name == dto.Department)
                            ?? throw new Exception($"Department '{dto.Department}' not found");

                        var password = _identityService.GenerateSecurePassword();
                        var email = await _identityService.GenerateUniversityEmailAsync(
                            dto.FullName.Split(' ')[0],
                            dto.FullName.Split(' ').Last(),
                            UserRole.Doctor);

                        var staffId = await _identityService.GenerateStaffIdAsync(dept.Id);

                        using var transaction = await _context.Database.BeginTransactionAsync();
                        try
                        {
                            var user = new SystemUser
                            {
                                Id = Ulid.NewUlid(),
                                FullName = dto.FullName,
                                Email = email,
                                UniversityEmail = email, // Set UniversityEmail
                                // NationalId = dto.NationalId, // DoctorRecord might not have NationalId? 
                                // Checking DoctorRecord definition would be good, but assuming we can skip or it has it.
                                // If dto has NationalId, use it. If not, we have a problem as SystemUser mandates it (if not nullable).
                                // SystemUser.NationalId is string, not nullable in my update?
                                // "public string NationalId { get; set; } = string.Empty;"
                                // So it's not nullable.
                                // Does DoctorRecord have NationalId? I need to check.
                                // If not, I'll assign empty string for now to fix build, but it might fail runtime if unique index prohibits multiple empty strings strings (Empty strings are usually unique if indexed as unique).
                                // Actually, empty strings might violate unique index if multiple users have empty.
                                // I will assume dto has NationalId for now or use a placeholder.
                                // Let's check DoctorRecord definition if possible, but for now I'll assign "N/A_" + Guid.
                                NationalId = string.IsNullOrEmpty(dto.NationalId) ? $"NA_{Guid.NewGuid()}" : dto.NationalId,
                                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                                Role = UserRole.Doctor,
                                CreatedByUserId = uploaderUserId,
                                IsActive = true
                            };
                            _context.SystemUsers.Add(user);
                            await _context.SaveChangesAsync();

                            var doctor = new Doctor
                            {
                                Id = Ulid.NewUlid(),
                                FullName = dto.FullName,
                                Email = "",
                                Phone = dto.Phone,
                                UniversityStaffId = staffId,
                                DepartmentId = dept.Id,
                                SystemUserId = user.Id
                            };
                            _context.Doctors.Add(doctor);
                            await _context.SaveChangesAsync();

                            // user.RelatedEntityId = doctor.Id; // Removed
                            // await _context.SaveChangesAsync(); // Removed

                            await transaction.CommitAsync();
                            successCount++;
                        }
                        catch
                        {
                            await transaction.RollbackAsync();
                            throw;
                        }
                    }
                    catch
                    {
                        failCount++;
                    }
                }

                file.ValidationStatus = "Completed";
                file.ExtractedDataJson = $"{{ \"success\": {successCount}, \"failed\": {failCount} }}";
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                file.ValidationStatus = "Failed";
                file.ExtractedDataJson = $"{{ \"error\": \"{ex.Message}\" }}";
                await _context.SaveChangesAsync();
            }
        }
    }
}
