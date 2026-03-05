using Microsoft.EntityFrameworkCore;
using UniversityManagementSystem.Core.DTOs;
using UniversityManagementSystem.Core.Entities;
using UniversityManagementSystem.Core.Interfaces;
using UniversityManagementSystem.Infrastructure.Data;
using NUlid;

namespace UniversityManagementSystem.Infrastructure.Services
{
    public class FileService(AppDbContext context, IAiService aiService, IAuditService auditService) : IFileService
    {
        private readonly AppDbContext _context = context;
        private readonly IAiService _aiService = aiService;
        private readonly IAuditService _auditService = auditService;


        public async Task<FileStatusDto?> GetFileStatusAsync(Ulid fileId)
        {
            var file = await _context.UploadedFiles.FindAsync(fileId);
            if (file == null) return null;

            return new FileStatusDto
            {
                Id = file.Id,
                FileName = file.FileName,
                Status = file.ValidationStatus,
                ExtractedData = file.ExtractedDataJson,
                Errors = file.ValidationErrors
            };
        }

        public async Task<IEnumerable<FileStatusDto>> GetUserFilesAsync(Ulid userId)
        {
            return await _context.UploadedFiles
                .Where(f => f.UploadedByUserId == userId)
                .Select(f => new FileStatusDto
                {
                    Id = f.Id,
                    FileName = f.FileName,
                    Status = f.ValidationStatus,
                    ExtractedData = f.ExtractedDataJson,
                    Errors = f.ValidationErrors
                })
                .ToListAsync();
        }

        public async Task<FileStatusDto> UploadFileStreamAsync(Ulid userId, Stream stream, string fileName, string contentType, long fileLength)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (string.IsNullOrEmpty(fileName)) throw new ArgumentException("Filename missing", nameof(fileName));

            var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "uploads");
            if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

            var uniqueFileName = $"{Guid.NewGuid()}_{fileName}";
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await stream.CopyToAsync(fileStream);
            }

            var uploadedFile = new UploadedFile
            {
                FileName = fileName,
                StoredPath = filePath,
                ContentType = contentType,
                FileSizeBytes = fileLength,
                UploadedByUserId = userId,
                ValidationStatus = "Processing",
                CreatedAt = DateTime.UtcNow
            };

            _context.UploadedFiles.Add(uploadedFile);
            await _context.SaveChangesAsync();

            return new FileStatusDto
            {
                Id = uploadedFile.Id,
                FileName = uploadedFile.FileName,
                Status = uploadedFile.ValidationStatus
            };
        }

        public async Task<FileStatusDto> UploadFileAsync(Ulid userId, UploadFileDto fileDto)
        {
            // 1. Save File to Disk
            var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "uploads");
            if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

            var uniqueFileName = $"{Guid.NewGuid()}_{fileDto.FileName}";
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            var fileBytes = Convert.FromBase64String(fileDto.Base64Content);
            await File.WriteAllBytesAsync(filePath, fileBytes);

            // 2. Create Entity
            var uploadedFile = new UploadedFile
            {
                FileName = fileDto.FileName,
                StoredPath = filePath,
                ContentType = fileDto.ContentType,
                FileSizeBytes = fileBytes.Length,
                UploadedByUserId = userId,
                ValidationStatus = "Processing"
            };
            _context.UploadedFiles.Add(uploadedFile);
            await _context.SaveChangesAsync();

            // 3. Trigger AI Extraction (Fire and Forget or Await? Await for simplicity here)
            // In production, this should be a background job.
            var extraction = await _aiService.ExtractDataFromFileAsync(filePath, fileDto.ContentType);

            uploadedFile.ValidationStatus = extraction.Success ? "Validated" : "Rejected";
            uploadedFile.ExtractedDataJson = extraction.ExtractectedJson;
            uploadedFile.ValidationErrors = extraction.Errors != null ? string.Join(", ", extraction.Errors) : null;

            await _context.SaveChangesAsync();

            return new FileStatusDto
            {
                Id = uploadedFile.Id,
                FileName = uploadedFile.FileName,
                Status = uploadedFile.ValidationStatus,
                ExtractedData = uploadedFile.ExtractedDataJson,
                Errors = uploadedFile.ValidationErrors
            };
        }

        public async Task RenameFileAsync(Ulid fileId, RenameFileDto dto)
        {
            var file = await _context.UploadedFiles.FindAsync(fileId)
                       ?? throw new KeyNotFoundException($"File {fileId} not found.");

            var oldValues = System.Text.Json.JsonSerializer.Serialize(new { file.FileName });

            file.FileName = dto.NewFileName;
            _context.Entry(file).State = EntityState.Modified;
            await _context.SaveChangesAsync();

            var newValues = System.Text.Json.JsonSerializer.Serialize(new { file.FileName });
            await _auditService.LogAsync("Rename", "UploadedFile", fileId.ToString(), oldValues, newValues, null);
        }

        public async Task DeleteFileAsync(Ulid fileId)
        {
            var file = await _context.UploadedFiles.FindAsync(fileId)
                       ?? throw new KeyNotFoundException($"File {fileId} not found.");

            var oldValues = System.Text.Json.JsonSerializer.Serialize(new { file.FileName, file.DeletedAt });

            file.DeletedAt = DateTime.UtcNow;
            _context.Entry(file).State = EntityState.Modified;
            await _context.SaveChangesAsync();

            await _auditService.LogAsync("SoftDelete", "UploadedFile", fileId.ToString(), oldValues, null, null);
        }
    }
}
