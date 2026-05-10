using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using NUlid;
using UniversityManagementSystem.Core.DTOs;
using UniversityManagementSystem.Core.Entities;
using UniversityManagementSystem.Core.Interfaces;
using UniversityManagementSystem.Infrastructure.Data;

namespace UniversityManagementSystem.Infrastructure.Services
{
    public class StudentFileService(
        AppDbContext context,
        IStorageService storage) : IStudentFileService
    {
        private readonly AppDbContext _context = context;
        private readonly IStorageService _storage = storage;
        private const string Folder = "student-files";

        // ── Upload ───────────────────────────────────────────────────────────
        public async Task<StudentFileUploadResponseDto> UploadAsync(Ulid studentId, IFormFile file)
        {
            // 1. Upload to R2
            string storageKey;
            using (var stream = file.OpenReadStream())
            {
                storageKey = await _storage.UploadAsync(stream, file.FileName, file.ContentType, Folder);
            }

            // 2. Extract text if applicable
            string? extractedText = null;
            var ct = file.ContentType.ToLower();
            var ext = Path.GetExtension(file.FileName).ToLower();

            if (ct == "text/plain" || ext == ".txt")
            {
                using var stream = file.OpenReadStream();
                using var reader = new StreamReader(stream, Encoding.UTF8);
                extractedText = await reader.ReadToEndAsync();
                // Trim if very long (safety guard: keep first 50k chars)
                if (extractedText.Length > 50_000)
                    extractedText = extractedText[..50_000];
            }
            // PDF extraction: we store the key and let the AI service fetch it via signed URL.
            // Full PDF text extraction (e.g. PdfPig) can be wired here in the future.

            // 3. Save record
            var record = new StudentFile
            {
                FileName = file.FileName,
                StorageKey = storageKey,
                ContentType = file.ContentType,
                FileSizeBytes = file.Length,
                UploadedByStudentId = studentId,
                ExtractedText = extractedText
            };
            _context.StudentFiles.Add(record);
            await _context.SaveChangesAsync();

            return new StudentFileUploadResponseDto
            {
                FileId = record.Id.ToString(),
                FileName = record.FileName,
                Size = record.FileSizeBytes,
                TextExtracted = extractedText != null
            };
        }

        // ── List ─────────────────────────────────────────────────────────────
        public async Task<IEnumerable<StudentFileDto>> GetMyFilesAsync(Ulid studentId)
        {
            return await _context.StudentFiles
                .Where(f => f.UploadedByStudentId == studentId && f.DeletedAt == null)
                .OrderByDescending(f => f.CreatedAt)
                .Select(f => new StudentFileDto
                {
                    FileId = f.Id.ToString(),
                    FileName = f.FileName,
                    ContentType = f.ContentType,
                    Size = f.FileSizeBytes,
                    HasExtractedText = f.ExtractedText != null,
                    CreatedAt = f.CreatedAt
                })
                .ToListAsync();
        }

        // ── Content for AI ───────────────────────────────────────────────────
        public async Task<(string content, bool isText)> GetFileContentForAiAsync(Ulid fileId, Ulid studentId)
        {
            var file = await _context.StudentFiles
                .FirstOrDefaultAsync(f => f.Id == fileId && f.DeletedAt == null)
                ?? throw new KeyNotFoundException($"File '{fileId}' not found.");

            // Security: students can only access their own files
            if (file.UploadedByStudentId != studentId)
                throw new UnauthorizedAccessException("You do not have access to this file.");

            // If text was extracted at upload time, return it directly
            if (!string.IsNullOrEmpty(file.ExtractedText))
                return (file.ExtractedText, true);

            // Otherwise return the public R2 URL so the AI service can fetch the raw file
            var publicUrl = _storage.BuildUrl(file.StorageKey);
            return (publicUrl, false);
        }
    }
}
