using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using UniversityManagementSystem.Core.DTOs;
using UniversityManagementSystem.Core.Entities;
using UniversityManagementSystem.Core.Interfaces;
using UniversityManagementSystem.Infrastructure.Data;
using NUlid;

namespace UniversityManagementSystem.Infrastructure.Services
{
    public class AcademicYearService(AppDbContext context, IAuditService auditService) : IAcademicYearService
    {
        private readonly AppDbContext _context = context;
        private readonly IAuditService _auditService = auditService;

        public async Task<AcademicYearDto> CreateAsync(CreateAcademicYearDto dto)
        {
            // Validate college exists
            var college = await _context.Colleges.FindAsync(dto.CollegeId)
                ?? throw new KeyNotFoundException($"College with ID {dto.CollegeId} not found.");

            // Enforce Order uniqueness per college (1–6 constraint is on entity constructor)
            var orderTaken = await _context.Set<AcademicYear>()
                .AnyAsync(y => y.CollegeId == dto.CollegeId && y.Order == dto.Order);
            if (orderTaken)
                throw new InvalidOperationException($"A year with Order={dto.Order} already exists for this college.");

            // If new year is active, deactivate others in the same college
            if (dto.IsActive)
            {
                var activeYears = await _context.Set<AcademicYear>()
                    .Where(y => y.CollegeId == dto.CollegeId && y.IsActive)
                    .ToListAsync();
                foreach (var y in activeYears) y.Deactivate();
            }

            var entity = new AcademicYear(dto.Name, dto.IsActive, dto.Order, dto.CollegeId);
            _context.Set<AcademicYear>().Add(entity);
            await _context.SaveChangesAsync();

            return MapToDto(entity, college.Name);
        }

        public async Task<IEnumerable<AcademicYearDto>> GetAllAsync()
        {
            var years = await _context.Set<AcademicYear>()
                .Include(y => y.College)
                .OrderBy(y => y.CollegeId)
                .ThenBy(y => y.Order)
                .ToListAsync();

            return years.Select(y => MapToDto(y, y.College.Name));
        }

        public async Task<IEnumerable<AcademicYearDto>> GetByCollegeIdAsync(Ulid collegeId)
        {
            var years = await _context.Set<AcademicYear>()
                .Include(y => y.College)
                .Where(y => y.CollegeId == collegeId)
                .OrderBy(y => y.Order)
                .ToListAsync();

            return years.Select(y => MapToDto(y, y.College.Name));
        }

        public async Task ActivateAsync(Ulid id)
        {
            var yearToActivate = await _context.Set<AcademicYear>()
                .Include(y => y.College)
                .FirstOrDefaultAsync(y => y.Id == id)
                ?? throw new KeyNotFoundException($"Academic Year with ID {id} not found.");

            yearToActivate.Activate();

            // Deactivate all others in the same college
            var otherActiveYears = await _context.Set<AcademicYear>()
                .Where(y => y.CollegeId == yearToActivate.CollegeId && y.IsActive && y.Id != id)
                .ToListAsync();
            foreach (var y in otherActiveYears) y.Deactivate();

            await _context.SaveChangesAsync();
        }

        public async Task<AcademicYearDto> UpdateAsync(Ulid id, UpdateAcademicYearDto dto)
        {
            var year = await _context.Set<AcademicYear>()
                .Include(y => y.College)
                .FirstOrDefaultAsync(y => y.Id == id)
                ?? throw new KeyNotFoundException($"Academic Year with ID {id} not found.");

            // Enforce Order uniqueness per college when changing order
            if (year.Order != dto.Order)
            {
                var orderTaken = await _context.Set<AcademicYear>()
                    .AnyAsync(y => y.CollegeId == year.CollegeId && y.Order == dto.Order && y.Id != id);
                if (orderTaken)
                    throw new InvalidOperationException($"A year with Order={dto.Order} already exists for this college.");
            }

            var oldValues = System.Text.Json.JsonSerializer.Serialize(new { year.Name, year.IsActive, year.Order });

            if (dto.IsActive && !year.IsActive)
            {
                var activeYears = await _context.Set<AcademicYear>()
                    .Where(y => y.CollegeId == year.CollegeId && y.IsActive && y.Id != id)
                    .ToListAsync();
                foreach (var y in activeYears) y.Deactivate();
            }

            year.Update(dto.Name, dto.IsActive, dto.Order);

            _context.Entry(year).State = EntityState.Modified;
            await _context.SaveChangesAsync();

            var newValues = System.Text.Json.JsonSerializer.Serialize(new { year.Name, year.IsActive, year.Order });
            await _auditService.LogAsync("Update", "AcademicYear", id.ToString(), oldValues, newValues, null);

            return MapToDto(year, year.College.Name);
        }

        public async Task DeleteAsync(Ulid id)
        {
            var year = await _context.Set<AcademicYear>().FindAsync(id)
                ?? throw new KeyNotFoundException($"Academic Year with ID {id} not found.");

            var hasSemesters = await _context.Set<Semester>().AnyAsync(s => s.AcademicYearId == id);
            if (hasSemesters)
                throw new InvalidOperationException("Cannot delete Academic Year because it has associated semesters.");

            var oldValues = System.Text.Json.JsonSerializer.Serialize(new { year.Name, year.IsActive, year.Order, year.DeletedAt });

            year.DeletedAt = DateTime.UtcNow;
            _context.Entry(year).State = EntityState.Modified;
            await _context.SaveChangesAsync();

            await _auditService.LogAsync("SoftDelete", "AcademicYear", id.ToString(), oldValues, null, null);
        }

        private static AcademicYearDto MapToDto(AcademicYear entity, string collegeName = "")
        {
            return new AcademicYearDto
            {
                Id          = entity.Id,
                Name        = entity.Name,
                IsActive    = entity.IsActive,
                Order       = entity.Order,
                CollegeId   = entity.CollegeId,
                CollegeName = collegeName
            };
        }
    }
}
