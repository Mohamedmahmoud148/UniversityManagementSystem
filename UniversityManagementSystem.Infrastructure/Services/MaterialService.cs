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

namespace UniversityManagementSystem.Infrastructure.Services
{
    public class MaterialService(AppDbContext context) : IMaterialService
    {
        private const string MaterialsFolder = "wwwroot/materials";

        public async Task<MaterialDto> UploadMaterialAsync(int offeringId, int doctorId, IFormFile file)
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

            // 2. Prepare Storage
            var uploadPath = Path.Combine(Directory.GetCurrentDirectory(), MaterialsFolder);
            if (!Directory.Exists(uploadPath))
                Directory.CreateDirectory(uploadPath);

            var storedFileName = $"{Guid.NewGuid()}_{file.FileName}";
            var filePath = Path.Combine(uploadPath, storedFileName);

            // 3. Save File
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            // 4. Save Entity
            var material = new Material
            {
                FileName = file.FileName,
                StoredFileName = storedFileName,
                ContentType = file.ContentType,
                FileSize = file.Length,
                UploadedAt = DateTime.UtcNow,
                SubjectOfferingId = offeringId,
                UploadedByDoctorId = doctorId
            };

            context.Materials.Add(material);
            await context.SaveChangesAsync();

            return new MaterialDto
            {
                Id = material.Id,
                FileName = material.FileName,
                ContentType = material.ContentType,
                FileSize = material.FileSize,
                UploadedAt = material.UploadedAt
            };
        }

        public async Task DeleteMaterialAsync(int materialId, int doctorId)
        {
            var material = await context.Materials
                .Include(m => m.SubjectOffering)
                .FirstOrDefaultAsync(m => m.Id == materialId)
                ?? throw new KeyNotFoundException($"Material with ID {materialId} not found.");

            if (material.UploadedByDoctorId != doctorId)
                throw new UnauthorizedAccessException("You are not authorized to delete this material.");

            // 1. Delete File from Disk
            var filePath = Path.Combine(Directory.GetCurrentDirectory(), MaterialsFolder, material.StoredFileName);
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }

            // 2. Delete Entity
            context.Materials.Remove(material);
            await context.SaveChangesAsync();
        }

        public async Task<PaginatedMaterialResponseDto> GetMaterialsByOfferingAsync(int offeringId, int studentId, int page, int pageSize, string? search)
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
            {
                query = query.Where(m => m.FileName.Contains(search));
            }

            var totalCount = await query.CountAsync();

            var items = await query
                .OrderByDescending(m => m.UploadedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(m => new MaterialDto
                {
                    Id = m.Id,
                    FileName = m.FileName,
                    ContentType = m.ContentType,
                    FileSize = m.FileSize,
                    UploadedAt = m.UploadedAt
                })
                .ToListAsync();

            return new PaginatedMaterialResponseDto
            {
                TotalCount = totalCount,
                PageNumber = page,
                PageSize = pageSize,
                Items = items
            };
        }

        public async Task<(string FilePath, string ContentType, string FileName, DateTime LastModified)> GetMaterialFileInfoAsync(int materialId, int studentId)
        {
            var material = await context.Materials
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.Id == materialId)
                ?? throw new KeyNotFoundException($"Material with ID {materialId} not found.");

            // 1. Validate Enrollment
            var isEnrolled = await context.Enrollments
                .AsNoTracking()
                .AnyAsync(e => e.StudentId == studentId && e.SubjectOfferingId == material.SubjectOfferingId);

            if (!isEnrolled)
                throw new UnauthorizedAccessException("You are not enrolled in this course.");

            // 2. Get File Info
            var filePath = Path.Combine(Directory.GetCurrentDirectory(), MaterialsFolder, material.StoredFileName);
            
            if (!File.Exists(filePath))
                throw new FileNotFoundException("File not found on server.");

            // Since we upgraded to PhysicalFileResult, we return path instead of stream
            return (filePath, material.ContentType, material.FileName, material.UploadedAt);
        }
    }
}
