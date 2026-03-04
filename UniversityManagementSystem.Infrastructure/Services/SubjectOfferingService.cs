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
    public class SubjectOfferingService(AppDbContext context) : ISubjectOfferingService
    {
        private readonly AppDbContext _context = context;

        public async Task<SubjectOfferingDto> CreateAsync(CreateSubjectOfferingDto dto)
        {
            // 1. Validate Dependencies
            var subjectExists = await _context.Set<Subject>().AnyAsync(s => s.Id == dto.SubjectId);
            if (!subjectExists) throw new KeyNotFoundException($"Subject with ID {dto.SubjectId} not found.");

            var semesterExists = await _context.Set<Semester>().AnyAsync(s => s.Id == dto.SemesterId);
            if (!semesterExists) throw new KeyNotFoundException($"Semester with ID {dto.SemesterId} not found.");

            var doctorExists = await _context.Set<Doctor>().AnyAsync(d => d.Id == dto.DoctorId);
            if (!doctorExists) throw new KeyNotFoundException($"Doctor with ID {dto.DoctorId} not found.");

            // Validate Hierarchy
            var deptExists = await _context.Departments.AnyAsync(d => d.Id == dto.DepartmentId);
            if (!deptExists) throw new KeyNotFoundException($"Department with ID {dto.DepartmentId} not found.");

            var batchExists = await _context.Batches.AnyAsync(b => b.Id == dto.BatchId && b.DepartmentId == dto.DepartmentId);
            if (!batchExists) throw new KeyNotFoundException($"Batch {dto.BatchId} not found or mismatch with Department.");

            if (dto.GroupId.HasValue)
            {
                var groupExists = await _context.Groups.AnyAsync(g => g.Id == dto.GroupId && g.BatchId == dto.BatchId);
                if (!groupExists) throw new KeyNotFoundException($"Group {dto.GroupId} not found or mismatch with Batch.");
            }

            // 2. Prevent Duplicates (Same Subject + Same Semester)
            var exists = await _context.Set<SubjectOffering>()
                .AnyAsync(so => so.SubjectId == dto.SubjectId && so.SemesterId == dto.SemesterId);

            if (exists)
                throw new InvalidOperationException("This subject is already offered in the specified semester.");

            // 3. Create Entity
            var entity = new SubjectOffering(dto.SubjectId, dto.SemesterId, dto.DoctorId, dto.DepartmentId, dto.BatchId, dto.GroupId, dto.MaxCapacity);

            _context.Set<SubjectOffering>().Add(entity);
            await _context.SaveChangesAsync();

            // 4. Return DTO (Need to fetch includes for names)
            return await GetByIdAsync(entity.Id);
        }

        public async Task<IEnumerable<SubjectOfferingDto>> GetBySemesterAsync(int semesterId)
        {
            var offerings = await _context.Set<SubjectOffering>()
                .Include(so => so.Subject)
                .Include(so => so.Semester)
                .Include(so => so.Doctor)
                .Where(so => so.SemesterId == semesterId)
                .ToListAsync();

            return offerings.Select(MapToDto);
        }

        public async Task<SubjectOfferingDto?> GetByPublicIdAsync(string publicId)
        {
            var entity = await _context.Set<SubjectOffering>()
                .Include(so => so.Subject)
                .Include(so => so.Semester)
                .Include(so => so.Doctor)
                .FirstOrDefaultAsync(so => so.PublicId == publicId);

            return entity == null ? null : MapToDto(entity);
        }

        public async Task<IEnumerable<SubjectOfferingDto>> GetByDoctorAsync(int systemUserId)
        {
            // 1. Find Doctor Entity by SystemUserId
            var doctor = await _context.Set<Doctor>()
                .FirstOrDefaultAsync(d => d.SystemUserId == systemUserId);

            if (doctor == null)
                return []; // Or throw, depends on requirement. Returning empty list is safer.

            // 2. Get Offerings
            var offerings = await _context.Set<SubjectOffering>()
                .Include(so => so.Subject)
                .Include(so => so.Semester)
                .Include(so => so.Doctor)
                .Where(so => so.DoctorId == doctor.Id)
                .ToListAsync();

            return offerings.Select(MapToDto);
        }

        public async Task<IEnumerable<SubjectOfferingDto>> GetByStudentAsync(int studentId)
        {
            var enrollmentIds = await _context.Enrollments
               .Where(e => e.StudentId == studentId && e.IsActive)
               .Select(e => e.SubjectOfferingId)
               .ToListAsync();

            if (enrollmentIds.Count == 0) return [];

            var offerings = await _context.Set<SubjectOffering>()
                .Include(so => so.Subject)
                .Include(so => so.Semester)
                .Include(so => so.Doctor)
                .Where(so => enrollmentIds.Contains(so.Id))
                .ToListAsync();

            return offerings.Select(MapToDto);
        }

        private async Task<SubjectOfferingDto> GetByIdAsync(int id)
        {
            var entity = await _context.Set<SubjectOffering>()
                .Include(so => so.Subject)
                .Include(so => so.Semester)
                .Include(so => so.Doctor)
                .FirstOrDefaultAsync(so => so.Id == id)
                ?? throw new KeyNotFoundException($"Offering {id} not found.");

            return MapToDto(entity);
        }

        private static SubjectOfferingDto MapToDto(SubjectOffering entity)
        {
            return new SubjectOfferingDto
            {
                Id = entity.Id,
                PublicId = entity.PublicId,
                SubjectId = entity.SubjectId,
                SubjectName = entity.Subject?.Name ?? string.Empty,
                SemesterId = entity.SemesterId,
                SemesterName = entity.Semester?.Name ?? string.Empty,
                DoctorId = entity.DoctorId,
                DoctorName = entity.Doctor?.FullName ?? string.Empty,
                MaxCapacity = entity.MaxCapacity,
                DepartmentId = entity.DepartmentId,
                BatchId = entity.BatchId,
                GroupId = entity.GroupId
            };
        }
    }
}
