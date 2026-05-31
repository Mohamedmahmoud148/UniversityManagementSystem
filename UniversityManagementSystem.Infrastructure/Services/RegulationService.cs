using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UniversityManagementSystem.Core.Entities;
using UniversityManagementSystem.Core.Interfaces;
using UniversityManagementSystem.Infrastructure.Data;
using NUlid;

namespace UniversityManagementSystem.Infrastructure.Services
{
    public class RegulationService(AppDbContext context) : IRegulationService
    {
        private readonly AppDbContext _context = context;

        // ── Slugify helper ──────────────────────────────────────────────────────
        /// <summary>
        /// Converts a title into a URL-safe slug, e.g.
        /// "General Academic Rules" → "general-academic-rules"
        /// Guarantees uniqueness by appending "-2", "-3" … if needed.
        /// </summary>
        private static string Slugify(string title)
        {
            var slug = title.ToLowerInvariant().Trim();
            // Replace Arabic/non-ASCII with nothing (keep Latin + digits)
            slug = Regex.Replace(slug, @"[^\u0000-\u007E]", "");
            // Replace any non-alphanumeric char with hyphen
            slug = Regex.Replace(slug, @"[^a-z0-9]+", "-");
            // Trim leading / trailing hyphens
            slug = slug.Trim('-');
            return string.IsNullOrEmpty(slug) ? "regulation" : slug;
        }

        private async Task<string> GenerateUniqueCodeAsync(string title)
        {
            var baseSlug = Slugify(title);
            if (!await _context.Regulations.AnyAsync(r => r.Code == baseSlug))
                return baseSlug;

            for (int suffix = 2; suffix <= 999; suffix++)
            {
                var candidate = $"{baseSlug}-{suffix}";
                if (!await _context.Regulations.AnyAsync(r => r.Code == candidate))
                    return candidate;
            }
            // Fallback — extremely unlikely
            return $"{baseSlug}-{Guid.NewGuid():N}";
        }

        // ── Interface Implementations ───────────────────────────────────────────
        public async Task<IEnumerable<Regulation>> GetAllAsync()
            => await _context.Regulations
                .Include(r => r.File)
                .Include(r => r.RegulationSubjects)
                .Where(r => r.DeletedAt == null)
                .ToListAsync();

        public async Task<IEnumerable<Regulation>> GetActiveAsync()
            => await _context.Regulations
                .Include(r => r.File)
                .Include(r => r.RegulationSubjects)
                .Where(r => r.IsActive && r.DeletedAt == null)
                .ToListAsync();

        /// <summary>NEW: Resolve a Regulation by its auto-generated slug code.</summary>
        public async Task<Regulation?> GetByCodeAsync(string code)
            => await _context.Regulations
                .Include(r => r.RegulationSubjects)
                .FirstOrDefaultAsync(r => r.Code == code);

        public async Task<IEnumerable<Regulation>> GetByDepartmentAsync(Ulid departmentId)
            => await _context.Regulations
                .Include(r => r.RegulationSubjects)
                .Where(r => r.DepartmentId == departmentId)
                .ToListAsync();

        public async Task<Regulation?> GetForStudentAsync(Ulid studentId)
        {
            var student = await _context.Students.FindAsync(studentId);
            if (student == null || !student.RegulationId.HasValue) return null;

            return await _context.Regulations
                .Include(r => r.RegulationSubjects)
                .FirstOrDefaultAsync(r => r.Id == student.RegulationId.Value);
        }

        public async Task<Regulation> CreateAsync(Regulation regulation)
        {
            return await CreateWithSubjectsAsync(regulation, []);
        }

        public async Task<Regulation> CreateWithSubjectsAsync(Regulation regulation, IEnumerable<RegulationSubject> subjects)
        {
            // Auto-generate Code from Title if not already set
            if (string.IsNullOrWhiteSpace(regulation.Code))
                regulation.Code = await GenerateUniqueCodeAsync(regulation.Title);

            foreach (var subject in subjects)
            {
                regulation.RegulationSubjects.Add(subject);
            }

            _context.Regulations.Add(regulation);

            // Assign to latest batch in department if DepartmentId is present
            if (regulation.DepartmentId.HasValue)
            {
                var latestBatch = await _context.Batches
                    .Where(b => b.DepartmentId == regulation.DepartmentId.Value)
                    .OrderByDescending(b => b.CreatedAt)
                    .FirstOrDefaultAsync();

                if (latestBatch != null)
                {
                    latestBatch.RegulationId = regulation.Id;
                    
                    // Update all students in this batch
                    var studentsInBatch = await _context.Students
                        .Where(s => s.BatchId == latestBatch.Id)
                        .ToListAsync();
                    
                    foreach (var student in studentsInBatch)
                    {
                        student.RegulationId = regulation.Id;
                    }
                }
            }

            await _context.SaveChangesAsync();
            return regulation;
        }

        public async Task UpdateAsync(Ulid id, Regulation regulation)
        {
            await UpdateWithSubjectsAsync(id, regulation, null);
        }

        public async Task UpdateWithSubjectsAsync(Ulid id, Regulation regulation, IEnumerable<RegulationSubject>? subjects)
        {
            var existing = await _context.Regulations
                .Include(r => r.RegulationSubjects)
                .FirstOrDefaultAsync(r => r.Id == id);
                
            if (existing == null) return;

            existing.Title = regulation.Title;
            existing.Content = regulation.Content;
            existing.Type = regulation.Type;
            existing.IsActive = regulation.IsActive;
            existing.DepartmentId = regulation.DepartmentId;

            // Re-generate slug if title changed significantly and code hasn't been customized
            if (existing.Code == Slugify(existing.Title) && existing.Title != regulation.Title)
                existing.Code = await GenerateUniqueCodeAsync(regulation.Title);

            if (subjects != null)
            {
                // Clear and replace subjects
                _context.RegulationSubjects.RemoveRange(existing.RegulationSubjects);
                existing.RegulationSubjects.Clear();
                
                foreach (var subject in subjects)
                {
                    subject.RegulationId = existing.Id;
                    existing.RegulationSubjects.Add(subject);
                }
            }

            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(Ulid id)
        {
            var existing = await _context.Regulations.FindAsync(id);
            if (existing == null) return;

            existing.DeletedAt = System.DateTime.UtcNow;
            _context.Entry(existing).State = EntityState.Modified;
            await _context.SaveChangesAsync();
        }
    }
}
