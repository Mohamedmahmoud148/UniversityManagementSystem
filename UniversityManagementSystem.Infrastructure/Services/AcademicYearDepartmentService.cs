using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using UniversityManagementSystem.Core.DTOs;
using UniversityManagementSystem.Core.Entities;
using UniversityManagementSystem.Core.Interfaces;
using UniversityManagementSystem.Infrastructure.Data;
using NUlid;

namespace UniversityManagementSystem.Infrastructure.Services
{
    public class AcademicYearDepartmentService(
        AppDbContext context,
        ILogger<AcademicYearDepartmentService> logger) : IAcademicYearDepartmentService
    {
        private readonly AppDbContext _context = context;
        private readonly ILogger<AcademicYearDepartmentService> _logger = logger;

        // ── Helpers ──────────────────────────────────────────────────────────────

        private static AcademicYearDepartmentDto MapToDto(AcademicYearDepartment m) => new()
        {
            MappingId      = m.Id,
            AcademicYearId = m.AcademicYearId,
            YearName       = m.AcademicYear?.Name ?? string.Empty,
            DepartmentId   = m.DepartmentId,
            DepartmentName = m.Department?.Name ?? string.Empty,
            IsActive       = m.IsActive
        };

        /// <summary>
        /// Base query for a year's mappings — no soft-delete filter needed because
        /// AcademicYearDepartment is NOT a BaseEntity (hard-delete only).
        /// </summary>
        private IQueryable<AcademicYearDepartment> MappingsForYear(Ulid yearId)
            => _context.AcademicYearDepartments
                .Include(m => m.AcademicYear)
                .Include(m => m.Department)
                .Where(m => m.AcademicYearId == yearId);

        // ── Public Methods ────────────────────────────────────────────────────────

        public async Task<IEnumerable<AcademicYearDepartmentDto>> GetActiveDepartmentsForYearAsync(Ulid yearId)
        {
            var yearExists = await _context.Set<AcademicYear>().AnyAsync(y => y.Id == yearId);
            if (!yearExists)
                throw new KeyNotFoundException($"Academic Year with ID {yearId} not found.");

            var mappings = await MappingsForYear(yearId)
                .Where(m => m.IsActive)
                .OrderBy(m => m.Department.Name)
                .ToListAsync();

            return mappings.Select(MapToDto);
        }

        public async Task<IEnumerable<AcademicYearDepartmentDto>> GetAllMappingsForYearAsync(Ulid yearId)
        {
            var yearExists = await _context.Set<AcademicYear>().AnyAsync(y => y.Id == yearId);
            if (!yearExists)
                throw new KeyNotFoundException($"Academic Year with ID {yearId} not found.");

            var mappings = await MappingsForYear(yearId)
                .OrderBy(m => m.IsActive ? 0 : 1)   // active first
                .ThenBy(m => m.Department.Name)
                .ToListAsync();

            return mappings.Select(MapToDto);
        }

        public async Task<AcademicYearDepartmentDto> AssignDepartmentAsync(Ulid yearId, AssignDepartmentToYearDto dto)
        {
            // 1. Resolve year
            var year = await _context.Set<AcademicYear>()
                .FirstOrDefaultAsync(y => y.Id == yearId)
                ?? throw new KeyNotFoundException($"Academic Year with ID {yearId} not found.");

            // 2. Resolve department
            var department = await _context.Departments.FindAsync(dto.DepartmentId)
                ?? throw new KeyNotFoundException($"Department with ID {dto.DepartmentId} not found.");

            // 3. ── College Integrity Check ──────────────────────────────────────
            //    Department must belong to the same college as the academic year.
            if (department.CollegeId != year.CollegeId)
            {
                _logger.LogWarning(
                    "Assign rejected: Department {DeptId} (College {DeptCollege}) does not match AcademicYear {YearId} (College {YearCollege})",
                    dto.DepartmentId, department.CollegeId, yearId, year.CollegeId);

                throw new InvalidOperationException(
                    $"Department '{department.Name}' belongs to a different college than Academic Year '{year.Name}'. " +
                    "A department can only be assigned to an academic year within the same college.");
            }

            // 4. Duplicate check — no IgnoreQueryFilters() needed because AcademicYearDepartment
            //    has no soft-delete filter (it is NOT a BaseEntity). A previously removed mapping
            //    is truly gone from the database, so re-assignment is always clean.
            var duplicate = await _context.AcademicYearDepartments
                .AnyAsync(m => m.AcademicYearId == yearId && m.DepartmentId == dto.DepartmentId);

            if (duplicate)
                throw new InvalidOperationException(
                    $"Department '{department.Name}' is already assigned to this academic year.");

            // 5. Create mapping
            var mapping = new AcademicYearDepartment
            {
                AcademicYearId = yearId,
                DepartmentId   = dto.DepartmentId,
                IsActive       = dto.IsActive
            };

            _context.AcademicYearDepartments.Add(mapping);
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Department {DeptId} assigned to AcademicYear {YearId} (IsActive={IsActive})",
                dto.DepartmentId, yearId, dto.IsActive);

            // Reload navigation properties for the response DTO
            await _context.Entry(mapping).Reference(m => m.AcademicYear).LoadAsync();
            await _context.Entry(mapping).Reference(m => m.Department).LoadAsync();

            return MapToDto(mapping);
        }

        public async Task UpdateMappingAsync(Ulid mappingId, bool isActive)
        {
            var mapping = await _context.AcademicYearDepartments.FindAsync(mappingId)
                ?? throw new KeyNotFoundException($"Mapping with ID {mappingId} not found.");

            // EF change-tracker detects the property change automatically — no manual
            // EntityState.Modified needed.
            mapping.IsActive = isActive;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Mapping {MappingId} IsActive set to {IsActive}", mappingId, isActive);
        }

        public async Task RemoveMappingAsync(Ulid mappingId)
        {
            var mapping = await _context.AcademicYearDepartments.FindAsync(mappingId)
                ?? throw new KeyNotFoundException($"Mapping with ID {mappingId} not found.");

            // True hard delete — AcademicYearDepartment does NOT inherit BaseEntity,
            // so the SaveChangesAsync soft-delete interceptor will NOT convert this
            // into a soft-delete. The row is permanently removed from the database.
            _context.AcademicYearDepartments.Remove(mapping);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Mapping {MappingId} permanently removed (hard delete)", mappingId);
        }
    }
}
