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
using UniversityManagementSystem.Infrastructure.Storage;
using NUlid;

namespace UniversityManagementSystem.Infrastructure.Services
{
    public class MaterialService(AppDbContext context, IStorageService storage, IFileService fileService) : IMaterialService
    {
        private const string StorageFolder = "materials";

        public async Task<MaterialDto> UploadMaterialAsync(Ulid offeringId, Ulid doctorId, IFormFile file)
        {
            if (file == null || file.Length == 0)
                throw new ArgumentException("File is empty.");

            // 1. Validate Offering & Doctor
            var offering = await context.Set<SubjectOffering>()
                .AsNoTracking()
                .FirstOrDefaultAsync(so => so.Id == offeringId)
                ?? throw new KeyNotFoundException($"SubjectOffering with ID {offeringId} not found.");

            if (offering.DoctorId != doctorId)
                throw new UnauthorizedAccessException("You are not the instructor for this offering.");

            // 2. Upload using FileService
            var fileId = await fileService.UploadFileStreamAsync(doctorId, file.OpenReadStream(), file.FileName, file.ContentType, file.Length);
            var uploadedFile = await context.UploadedFiles.FindAsync(fileId) 
                               ?? throw new InvalidOperationException("Failed to retrieve uploaded file.");
            var storageKey = uploadedFile.StorageKey;

            // 3. Save Entity — store the key; keep StoredFileName in sync for backward compatibility
            var material = new Material
            {
                FileName = file.FileName,
                StorageKey = storageKey,                       // New: the R2 object key
                StoredFileName = storageKey,                   // Legacy: mirrors StorageKey
                ContentType = file.ContentType,
                FileSize = file.Length,
                UploadedAt = DateTime.UtcNow,
                SubjectOfferingId = offeringId,
                UploadedByDoctorId = doctorId,
                FileId = fileId
            };

            context.Materials.Add(material);
            await context.SaveChangesAsync();

            return new MaterialDto
            {
                Id          = material.Id,
                FileName    = material.FileName,
                ContentType = material.ContentType,
                FileSize    = material.FileSize,
                UploadedAt  = material.UploadedAt,
                FileUrl     = storage.BuildUrl(material.StorageKey)
            };
        }

        public async Task DeleteMaterialAsync(Ulid materialId, Ulid doctorId)
        {
            var material = await context.Materials
                .Include(m => m.SubjectOffering)
                .FirstOrDefaultAsync(m => m.Id == materialId)
                ?? throw new KeyNotFoundException($"Material with ID {materialId} not found.");

            if (material.UploadedByDoctorId != doctorId)
                throw new UnauthorizedAccessException("You are not authorized to delete this material.");

            // 1. Delete from R2 — use StorageKey directly or delegate to fileService
            var key = !string.IsNullOrWhiteSpace(material.StorageKey)
                ? material.StorageKey
                : material.StoredFileName; // fallback for rows created before this fix
            
            // 2. Delete Entity
            context.Materials.Remove(material);
            await context.SaveChangesAsync();
            
            // Delete the related UploadedFile if it exists
            if (material.FileId.HasValue)
            {
                await fileService.DeleteFileAsync(material.FileId.Value);
            }
            else if (!string.IsNullOrWhiteSpace(key))
            {
                await storage.DeleteAsync(key);
            }
        }

        public async Task<PaginatedMaterialResponseDto> GetMaterialsByOfferingAsync(Ulid offeringId, Ulid studentId, int page, int pageSize, string? search)
        {
            // 1. Validate Enrollment
            var isEnrolled = await context.Enrollments
                .AsNoTracking()
                .AnyAsync(e => e.StudentId == studentId && e.SubjectOfferingId == offeringId);

            if (!isEnrolled)
                throw new UnauthorizedAccessException("You are not enrolled in this course.");

            // 2. Query
            var query = context.Materials
                .AsNoTracking()
                .Where(m => m.SubjectOfferingId == offeringId);

            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(m => m.FileName.Contains(search));

            var totalCount = await query.CountAsync();

            // ── Fetch from DB (include StorageKey for URL building) ───────────
            // BuildUrl is a pure in-memory call — EF Core cannot translate it to SQL,
            // so we project StorageKey out of the DB and map after ToListAsync.
            var rawItems = await query
                .OrderByDescending(m => m.UploadedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(m => new
                {
                    m.Id,
                    m.FileName,
                    m.ContentType,
                    m.FileSize,
                    m.UploadedAt,
                    m.StorageKey
                })
                .ToListAsync();

            // Build public CDN URL in memory — O(1) per item, no extra HTTP calls
            var items = rawItems.Select(m => new MaterialDto
            {
                Id          = m.Id,
                FileName    = m.FileName,
                ContentType = m.ContentType,
                FileSize    = m.FileSize,
                UploadedAt  = m.UploadedAt,
                FileUrl     = storage.BuildUrl(m.StorageKey)
            }).ToList();

            return new PaginatedMaterialResponseDto
            {
                TotalCount = totalCount,
                PageNumber = page,
                PageSize = pageSize,
                Items = items
            };
        }

        public async Task<string> GetMaterialUrlAsync(Ulid materialId, Ulid studentId)
        {
            var material = await context.Materials
                .Include(m => m.File)
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.Id == materialId)
                ?? throw new KeyNotFoundException($"Material with ID {materialId} not found.");

            // Validate Enrollment
            var isEnrolled = await context.Enrollments
                .AsNoTracking()
                .AnyAsync(e => e.StudentId == studentId && e.SubjectOfferingId == material.SubjectOfferingId);

            if (!isEnrolled)
                throw new UnauthorizedAccessException("You are not enrolled in this course.");

            var key = material.File?.StorageKey 
                      ?? (!string.IsNullOrWhiteSpace(material.StorageKey) ? material.StorageKey : material.StoredFileName);
                      
            return key; // Full R2 object key
        }

        /// <summary>
        /// Extracts the object key from a full R2 URL by removing the scheme+host prefix.
        /// e.g. "https://pub-xxx.r2.dev/materials/file.pdf" → "materials/file.pdf"
        /// </summary>
        private static string ExtractKeyFromUrl(string url)
        {
            if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
                return uri.AbsolutePath.TrimStart('/');
            return url;
        }
    }
}
