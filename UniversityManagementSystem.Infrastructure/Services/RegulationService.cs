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
            => await _context.Regulations.ToListAsync();

        public async Task<IEnumerable<Regulation>> GetActiveAsync()
            => await _context.Regulations.Where(r => r.IsActive).ToListAsync();

        /// <summary>NEW: Resolve a Regulation by its auto-generated slug code.</summary>
        public async Task<Regulation?> GetByCodeAsync(string code)
            => await _context.Regulations.FirstOrDefaultAsync(r => r.Code == code);

        public async Task<Regulation> CreateAsync(Regulation regulation)
        {
            // Auto-generate Code from Title if not already set
            if (string.IsNullOrWhiteSpace(regulation.Code))
                regulation.Code = await GenerateUniqueCodeAsync(regulation.Title);

            _context.Regulations.Add(regulation);
            await _context.SaveChangesAsync();
            return regulation;
        }

        public async Task UpdateAsync(Ulid id, Regulation regulation)
        {
            var existing = await _context.Regulations.FindAsync(id);
            if (existing == null) return;

            existing.Title = regulation.Title;
            existing.Content = regulation.Content;
            existing.Type = regulation.Type;
            existing.IsActive = regulation.IsActive;

            // Re-generate slug if title changed significantly and code hasn't been customized
            if (existing.Code == Slugify(existing.Title) && existing.Title != regulation.Title)
                existing.Code = await GenerateUniqueCodeAsync(regulation.Title);

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
