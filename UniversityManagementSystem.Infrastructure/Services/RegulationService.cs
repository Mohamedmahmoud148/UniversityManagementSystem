using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
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

        public async Task<IEnumerable<Regulation>> GetAllAsync()
        {
            return await _context.Regulations.ToListAsync();
        }

        public async Task<IEnumerable<Regulation>> GetActiveAsync()
        {
            return await _context.Regulations.Where(r => r.IsActive).ToListAsync();
        }

        public async Task<Regulation> CreateAsync(Regulation regulation)
        {
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
