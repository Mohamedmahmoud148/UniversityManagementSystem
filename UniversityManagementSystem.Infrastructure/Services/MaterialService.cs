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

        public async Task<MaterialDto> UploadMaterialAsync(Ulid offeringId, Ulid doctorId, IFormFile file, string title, string? description)
        {
            if (file == null || file.Length == 0)
                throw new ArgumentException("File is empty.");

            // 1. Validate Offering & Doctor
            // IgnoreQueryFilters: the offering itself is NOT soft-deleted;
            // the 404 was caused by EF propagating filters from related entities (e.g. Semester).
            var offering = await context.Set<SubjectOffering>()
                .AsNoTracking()
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(so => so.Id == offeringId && so.DeletedAt == null)
                ?? throw new KeyNotFoundException($"SubjectOffering with ID {offeringId} not found.");

            if (offering.DoctorId != doctorId)
                throw new UnauthorizedAccessException("You are not the instructor for this offering.");

            // Resolve the SystemUser ID so the FK on UploadedFiles is satisfied.
            // doctorId is the Doctor profile ID; UploadedByUserId must reference SystemUsers.
            var doctor = await context.Set<Doctor>()
                .AsNoTracking()
                .IgnoreQueryFilters()
                .Where(d => d.Id == doctorId && d.DeletedAt == null)
                .Select(d => new { d.SystemUserId })
                .FirstOrDefaultAsync()
                ?? throw new KeyNotFoundException($"Doctor with ID {doctorId} not found.");

            // 2. Upload using FileService
            var fileId = await fileService.UploadFileStreamAsync(doctor.SystemUserId, file.OpenReadStream(), file.FileName, file.ContentType, file.Length);
            var uploadedFile = await context.UploadedFiles.FindAsync(fileId) 
                               ?? throw new InvalidOperationException("Failed to retrieve uploaded file.");
            var storageKey = uploadedFile.StorageKey;

            // 3. Save Entity — store the key; keep StoredFileName in sync for backward compatibility
            var material = new Material
            {
                FileName = file.FileName,
                Title = title,
                Description = description,
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
                Title       = material.Title,
                Description = material.Description,
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

        public async Task<PaginatedMaterialResponseDto> GetMaterialsByOfferingAsync(
            Ulid offeringId,
            Ulid callerId,
            string callerRole,
            int page,
            int pageSize,
            string? search)
        {
            // ── Access Gate ───────────────────────────────────────────────────
            // Students: must be enrolled in the offering.
            // Doctors / Admins: bypass enrollment check — the JWT already enforces
            //   data-level permissions at the backend level.
            if (callerRole.Equals("Student", StringComparison.OrdinalIgnoreCase))
            {
                var isEnrolled = await context.Enrollments
                    .AsNoTracking()
                    .AnyAsync(e => e.StudentId == callerId && e.SubjectOfferingId == offeringId);

                if (!isEnrolled)
                    throw new UnauthorizedAccessException("You are not enrolled in this course.");
            }
            else
            {
                // Doctor / Admin: just confirm the offering exists.
                // IgnoreQueryFilters prevents false 404s when related entities (e.g. Semester)
                // have a soft-delete filter that EF Core propagates to the root query.
                var offeringExists = await context.SubjectOfferings
                    .AsNoTracking()
                    .IgnoreQueryFilters()
                    .AnyAsync(o => o.Id == offeringId && o.DeletedAt == null);

                if (!offeringExists)
                    throw new KeyNotFoundException($"SubjectOffering '{offeringId}' not found.");
            }

            // ── Query ─────────────────────────────────────────────────────────
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

        public async Task<string> GetMaterialUrlAsync(Ulid materialId, Ulid callerId, string callerRole)
        {
            var material = await context.Materials
                .Include(m => m.File)
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.Id == materialId)
                ?? throw new KeyNotFoundException($"Material with ID {materialId} not found.");

            // Students must be enrolled; doctors/admins bypass
            if (callerRole.Equals("Student", StringComparison.OrdinalIgnoreCase))
            {
                var isEnrolled = await context.Enrollments
                    .AsNoTracking()
                    .AnyAsync(e => e.StudentId == callerId && e.SubjectOfferingId == material.SubjectOfferingId);

                if (!isEnrolled)
                    throw new UnauthorizedAccessException("You are not enrolled in this course.");
            }

            var key = material.File?.StorageKey
                      ?? (!string.IsNullOrWhiteSpace(material.StorageKey) ? material.StorageKey : material.StoredFileName);

            return key;
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
