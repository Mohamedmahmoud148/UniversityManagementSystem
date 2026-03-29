using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using UniversityManagementSystem.Core.DTOs;
using UniversityManagementSystem.Core.Entities;
using UniversityManagementSystem.Core.Interfaces;
using UniversityManagementSystem.Infrastructure.Data;
using NUlid;

namespace UniversityManagementSystem.Infrastructure.Services
{
    public class FileService(AppDbContext context, IAiService aiService, IAuditService auditService, IStorageService storage) : IFileService
    {
        private readonly AppDbContext _context = context;
        private readonly IAiService _aiService = aiService;
        private readonly IAuditService _auditService = auditService;
        private readonly IStorageService _storage = storage;
        private const string StorageFolder = "files";


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

            // Upload to R2
            var fileUrl = await _storage.UploadAsync(stream, fileName, contentType, StorageFolder);

            var uploadedFile = new UploadedFile
            {
                FileName = fileName,
                StoredPath = fileUrl,       // R2 public URL
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
            // 1. Upload to R2 via MemoryStream
            var fileBytes = Convert.FromBase64String(fileDto.Base64Content);
            string fileUrl;
            using (var ms = new MemoryStream(fileBytes))
            {
                fileUrl = await _storage.UploadAsync(ms, fileDto.FileName, fileDto.ContentType, StorageFolder);
            }

            // 2. Create Entity — StoredPath holds the R2 URL
            var uploadedFile = new UploadedFile
            {
                FileName = fileDto.FileName,
                StoredPath = fileUrl,
                ContentType = fileDto.ContentType,
                FileSizeBytes = fileBytes.Length,
                UploadedByUserId = userId,
                ValidationStatus = "Processing"
            };
            _context.UploadedFiles.Add(uploadedFile);
            await _context.SaveChangesAsync();

            // 3. Trigger AI Extraction — pass URL as file reference
            var extraction = await _aiService.ExtractDataFromFileAsync(fileUrl, fileDto.ContentType);

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
