using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using UniversityManagementSystem.Core.DTOs;
using UniversityManagementSystem.Core.Entities;
using UniversityManagementSystem.Core.Interfaces;
using UniversityManagementSystem.Infrastructure.Data;

namespace UniversityManagementSystem.Infrastructure.Services
{
    public class AcademicYearService(AppDbContext context, IAuditService auditService) : IAcademicYearService
    {
        private readonly AppDbContext _context = context;
        private readonly IAuditService _auditService = auditService;

        public async Task<AcademicYearDto> CreateAsync(CreateAcademicYearDto dto)
        {
            // If new year is active, deactivate others
            if (dto.IsActive)
            {
                var activeYears = await _context.Set<AcademicYear>()
                    .Where(y => y.IsActive)
                    .ToListAsync();

                foreach (var year in activeYears)
                {
                    year.Deactivate();
                }
            }

            var entity = new AcademicYear(dto.Name, dto.IsActive);
            
            _context.Set<AcademicYear>().Add(entity);
            await _context.SaveChangesAsync();

            return MapToDto(entity);
        }

        public async Task<IEnumerable<AcademicYearDto>> GetAllAsync()
        {
            var years = await _context.Set<AcademicYear>()
                .OrderByDescending(y => y.Id)
                .ToListAsync();

            return years.Select(MapToDto);
        }

        public async Task ActivateAsync(int id)
        {
            var yearToActivate = await _context.Set<AcademicYear>().FindAsync(id);
            if (yearToActivate == null)
                throw new KeyNotFoundException($"Academic Year with ID {id} not found.");

            yearToActivate.Activate();

            // Deactivate all others
            var otherActiveYears = await _context.Set<AcademicYear>()
                .Where(y => y.IsActive && y.Id != id)
                .ToListAsync();

            foreach (var year in otherActiveYears)
            {
                year.Deactivate();
            }

            await _context.SaveChangesAsync();
        }

        public async Task<AcademicYearDto> UpdateAsync(int id, UpdateAcademicYearDto dto)
        {
            var year = await _context.Set<AcademicYear>().FindAsync(id);
            if (year == null)
                throw new KeyNotFoundException($"Academic Year with ID {id} not found.");

            var oldValues = System.Text.Json.JsonSerializer.Serialize(new { year.Name, year.IsActive });

            if (dto.IsActive && !year.IsActive)
            {
                // Deactivate others if this one is becoming active
                var activeYears = await _context.Set<AcademicYear>()
                    .Where(y => y.IsActive && y.Id != id)
                    .ToListAsync();

                foreach (var y in activeYears)
                {
                    y.Deactivate();
                }
            }

            year.Update(dto.Name, dto.IsActive);
            
            _context.Entry(year).State = EntityState.Modified;
            await _context.SaveChangesAsync();

            var newValues = System.Text.Json.JsonSerializer.Serialize(new { year.Name, year.IsActive });
            await _auditService.LogAsync("Update", "AcademicYear", id.ToString(), oldValues, newValues, null); // UserId can be passed from Controller
            
            return MapToDto(year);
        }

        public async Task DeleteAsync(int id)
        {
            var year = await _context.Set<AcademicYear>().FindAsync(id);
            if (year == null)
                throw new KeyNotFoundException($"Academic Year with ID {id} not found.");

            // Validate dependencies: Cannot delete if it has semesters
            var hasSemesters = await _context.Set<Semester>().AnyAsync(s => s.AcademicYearId == id);
            if (hasSemesters)
                throw new InvalidOperationException("Cannot delete Academic Year because it has associated semesters.");

            var oldValues = System.Text.Json.JsonSerializer.Serialize(new { year.Name, year.IsActive, year.DeletedAt });
            
            year.DeletedAt = DateTime.UtcNow;
            _context.Entry(year).State = EntityState.Modified;
            await _context.SaveChangesAsync();

            await _auditService.LogAsync("SoftDelete", "AcademicYear", id.ToString(), oldValues, null, null);
        }

        private static AcademicYearDto MapToDto(AcademicYear entity)
        {
            return new AcademicYearDto
            {
                Id = entity.Id,
                Name = entity.Name,
                IsActive = entity.IsActive
            };
        }
    }
}
