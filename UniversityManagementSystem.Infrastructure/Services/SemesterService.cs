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
    public class SemesterService(AppDbContext context, IAuditService auditService) : ISemesterService
    {
        private readonly AppDbContext _context = context;
        private readonly IAuditService _auditService = auditService;

        public async Task<SemesterDto> CreateAsync(CreateSemesterDto dto)
        {
            // 1. Validate AcademicYear exists
            var year = await _context.Set<AcademicYear>().FindAsync(dto.AcademicYearId);
            if (year == null)
                throw new KeyNotFoundException($"Academic Year with ID {dto.AcademicYearId} not found.");

            // 2. Validate Dates logic (End > Start) - already in Entity constructor, but good to check before DB call if needed.
            if (dto.EndDate <= dto.StartDate)
                throw new ArgumentException("End date must be after start date.");

            // 3. Check for Overlaps within the SAME Academic Year
            // Overlap formula: (StartA < EndB) and (EndA > StartB)
            var overlapExists = await _context.Set<Semester>()
                .Where(s => s.AcademicYearId == dto.AcademicYearId)
                .AnyAsync(s => dto.StartDate < s.EndDate && dto.EndDate > s.StartDate);

            if (overlapExists)
                throw new InvalidOperationException("Semester dates overlap with an existing semester in this academic year.");

            // 4. Create Entity
            var entity = new Semester(dto.Name, dto.AcademicYearId, dto.StartDate, dto.EndDate);
            
            _context.Set<Semester>().Add(entity);
            await _context.SaveChangesAsync();

            return MapToDto(entity, year.Name);
        }

        public async Task<IEnumerable<SemesterDto>> GetByAcademicYearAsync(int academicYearId)
        {
            var semesters = await _context.Set<Semester>()
                .Include(s => s.AcademicYear)
                .Where(s => s.AcademicYearId == academicYearId)
                .OrderBy(s => s.StartDate)
                .ToListAsync();

            return semesters.Select(s => MapToDto(s, s.AcademicYear.Name));
        }

        public async Task<SemesterDto> UpdateAsync(int id, UpdateSemesterDto dto)
        {
            var semester = await _context.Set<Semester>()
                .Include(s => s.AcademicYear)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (semester == null)
                throw new KeyNotFoundException($"Semester with ID {id} not found.");

            // Validate overlapping dates (excluding itself)
            var overlapExists = await _context.Set<Semester>()
                .Where(s => s.AcademicYearId == semester.AcademicYearId && s.Id != id)
                .AnyAsync(s => dto.StartDate < s.EndDate && dto.EndDate > s.StartDate);

            if (overlapExists)
                throw new InvalidOperationException("Semester dates overlap with another semester in the same year.");

            var oldValues = System.Text.Json.JsonSerializer.Serialize(new { semester.Name, semester.StartDate, semester.EndDate });
            
            semester.Update(dto.Name, dto.StartDate, dto.EndDate);
            
            _context.Entry(semester).State = EntityState.Modified;
            await _context.SaveChangesAsync();

            var newValues = System.Text.Json.JsonSerializer.Serialize(new { semester.Name, semester.StartDate, semester.EndDate });
            await _auditService.LogAsync("Update", "Semester", id.ToString(), oldValues, newValues, null);

            return MapToDto(semester, semester.AcademicYear.Name);
        }

        public async Task DeleteAsync(int id)
        {
            var semester = await _context.Set<Semester>().FindAsync(id);
            if (semester == null)
                throw new KeyNotFoundException($"Semester with ID {id} not found.");

            // Validate dependencies: Cannot delete if it has offerings
            var hasOfferings = await _context.Set<SubjectOffering>().AnyAsync(so => so.SemesterId == id);
            if (hasOfferings)
                throw new InvalidOperationException("Cannot delete Semester because it has associated subject offerings.");

            var oldValues = System.Text.Json.JsonSerializer.Serialize(new { semester.Name, semester.DeletedAt });

            semester.DeletedAt = DateTime.UtcNow;
            _context.Entry(semester).State = EntityState.Modified;
            await _context.SaveChangesAsync();

            await _auditService.LogAsync("SoftDelete", "Semester", id.ToString(), oldValues, null, null);
        }

        private static SemesterDto MapToDto(Semester entity, string academicYearName)
        {
            return new SemesterDto
            {
                Id = entity.Id,
                Name = entity.Name,
                AcademicYearId = entity.AcademicYearId,
                AcademicYearName = academicYearName,
                StartDate = entity.StartDate,
                EndDate = entity.EndDate
            };
        }
    }
}
