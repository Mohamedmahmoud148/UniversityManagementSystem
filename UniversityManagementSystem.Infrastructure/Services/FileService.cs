using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
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

        private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "application/pdf",
            "application/vnd.ms-excel",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "image/jpeg", "image/png", "image/gif", "image/webp",
            "text/plain", "text/csv"
        };

        private const long MaxFileSizeBytes = 20 * 1024 * 1024; // 20 MB

        private static void ValidateFile(string contentType, long length)
        {
            if (length == 0)
                throw new ArgumentException("File is empty.");
            if (length > MaxFileSizeBytes)
                throw new ArgumentException($"File exceeds maximum allowed size of {MaxFileSizeBytes / 1024 / 1024} MB.");
            if (!AllowedContentTypes.Contains(contentType))
                throw new ArgumentException($"File type '{contentType}' is not allowed.");
        }

        /// <inheritdoc/>
        public async Task<FileUploadResponseDto> UploadFormFileAsync(Ulid userId, IFormFile file)
        {
            ValidateFile(file.ContentType, file.Length);
            using var stream = file.OpenReadStream();
            var storageKey = await _storage.UploadAsync(stream, file.FileName, file.ContentType, StorageFolder);

            var uploadedFile = new UploadedFile
            {
                FileName = file.FileName,
                StorageKey = storageKey,
                ContentType = file.ContentType,
                FileSizeBytes = file.Length,
                UploadedByUserId = userId,
                ValidationStatus = "Ready",
                CreatedAt = DateTime.UtcNow
            };

            _context.UploadedFiles.Add(uploadedFile);
            await _context.SaveChangesAsync();

            // Return a public URL immediately so the caller can use the file
            var publicUrl = _storage.BuildUrl(storageKey);

            return new FileUploadResponseDto
            {
                FileId = uploadedFile.Id.ToString(),
                FileName = uploadedFile.FileName,
                ContentType = uploadedFile.ContentType,
                Size = uploadedFile.FileSizeBytes,
                Url = publicUrl
            };
        }


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

        public async Task<Ulid> UploadFileStreamAsync(Ulid userId, Stream stream, string fileName, string contentType, long fileLength)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (string.IsNullOrEmpty(fileName)) throw new ArgumentException("Filename missing", nameof(fileName));
            ValidateFile(contentType, fileLength);

            // Upload to R2 — returns object key
            var storageKey = await _storage.UploadAsync(stream, fileName, contentType, StorageFolder);

            var uploadedFile = new UploadedFile
            {
                FileName = fileName,
                StorageKey = storageKey,      // New: the R2 object key
                ContentType = contentType,
                FileSizeBytes = fileLength,
                UploadedByUserId = userId,
                ValidationStatus = "Processing",
                CreatedAt = DateTime.UtcNow
            };

            _context.UploadedFiles.Add(uploadedFile);
            await _context.SaveChangesAsync();

            return uploadedFile.Id;
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
