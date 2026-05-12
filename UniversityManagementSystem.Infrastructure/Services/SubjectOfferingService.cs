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
    public class SubjectOfferingService(AppDbContext context) : ISubjectOfferingService
    {
        private readonly AppDbContext _context = context;

        public async Task<SubjectOfferingDto> CreateAsync(CreateSubjectOfferingDto dto)
        {
            // 1. Resolve Dependencies from Codes
            var subject = await _context.Subjects.FirstOrDefaultAsync(s => s.Code == dto.SubjectCode);
            if (subject == null) throw new KeyNotFoundException($"Subject with code {dto.SubjectCode} not found.");

            var semesterIdUlid = Ulid.TryParse(dto.SemesterId, out var parsed) ? parsed : Ulid.Empty;
            var semester = await _context.Set<Semester>().FirstOrDefaultAsync(s => s.Id == semesterIdUlid);
            if (semester == null) throw new KeyNotFoundException($"Semester with ID {dto.SemesterId} not found.");

            var doctor = await _context.Doctors.FirstOrDefaultAsync(d => d.Code == dto.DoctorCode);
            if (doctor == null) throw new KeyNotFoundException($"Doctor with code {dto.DoctorCode} not found.");

            // Validate Hierarchy
            var dept = await _context.Departments.FirstOrDefaultAsync(d => d.Code == dto.DepartmentCode);
            if (dept == null) throw new KeyNotFoundException($"Department with code {dto.DepartmentCode} not found.");

            var batch = await _context.Batches.FirstOrDefaultAsync(b => b.Code == dto.BatchCode && b.DepartmentId == dept.Id);
            if (batch == null) throw new KeyNotFoundException($"Batch {dto.BatchCode} not found or mismatch with Department.");

            Ulid? groupId = null;
            if (!string.IsNullOrWhiteSpace(dto.GroupCode))
            {
                var group = await _context.Groups.FirstOrDefaultAsync(g => g.Code == dto.GroupCode && g.BatchId == batch.Id);
                if (group == null) throw new KeyNotFoundException($"Group {dto.GroupCode} not found or mismatch with Batch.");
                groupId = group.Id;
            }

            // 2. Prevent Duplicates (Same Subject + Same Semester)
            var exists = await _context.Set<SubjectOffering>()
                .AnyAsync(so => so.SubjectId == subject.Id && so.SemesterId == semester.Id);

            if (exists)
                throw new InvalidOperationException("This subject is already offered in the specified semester.");

            // 3. Create Entity
            var entity = new SubjectOffering(subject.Id, semester.Id, doctor.Id, dept.Id, batch.Id, groupId, dto.MaxCapacity);

            _context.Set<SubjectOffering>().Add(entity);
            await _context.SaveChangesAsync();

            // 4. Return DTO (Need to fetch includes for names)
            return await GetByIdAsync(entity.Id);
        }

        public async Task<IEnumerable<SubjectOfferingDto>> GetBySemesterAsync(Ulid semesterId)
        {
            var offerings = await _context.Set<SubjectOffering>()
                .Include(so => so.Subject)
                .Include(so => so.Semester)
                .Include(so => so.Doctor)
                .Include(so => so.Department)
                .Include(so => so.Batch)
                .Where(so => so.SemesterId == semesterId)
                .ToListAsync();

            return offerings.Select(MapToDto);
        }

        public async Task<SubjectOfferingDto?> GetByCodeAsync(string code)
        {
            var entity = await _context.Set<SubjectOffering>()
                .Include(so => so.Subject)
                .Include(so => so.Semester)
                .Include(so => so.Doctor)
                .Include(so => so.Department)
                .Include(so => so.Batch)
                .FirstOrDefaultAsync(so => so.Code == code);

            return entity == null ? null : MapToDto(entity);
        }

        public async Task<IEnumerable<SubjectOfferingDto>> GetByDoctorAsync(Ulid systemUserId)
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
                .Include(so => so.Department)
                .Include(so => so.Batch)
                .Where(so => so.DoctorId == doctor.Id)
                .ToListAsync();

            return offerings.Select(MapToDto);
        }

        public async Task<IEnumerable<SubjectOfferingDto>> GetByStudentAsync(Ulid studentId)
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
                .Include(so => so.Department)
                .Include(so => so.Batch)
                .Where(so => enrollmentIds.Contains(so.Id))
                .ToListAsync();

            return offerings.Select(MapToDto);
        }

        private async Task<SubjectOfferingDto> GetByIdAsync(Ulid id)
        {
            var entity = await _context.Set<SubjectOffering>()
                .Include(so => so.Subject)
                .Include(so => so.Semester)
                .Include(so => so.Doctor)
                .Include(so => so.Department)
                .Include(so => so.Batch)
                .FirstOrDefaultAsync(so => so.Id == id)
                ?? throw new KeyNotFoundException($"Offering {id} not found.");

            return MapToDto(entity);
        }

        private static SubjectOfferingDto MapToDto(SubjectOffering entity)
        {
            return new SubjectOfferingDto
            {
                Id = entity.Id,
                SubjectId = entity.SubjectId,
                SubjectCode = entity.Subject?.Code ?? string.Empty,
                SubjectName = entity.Subject?.Name ?? string.Empty,
                CreditHours = entity.Subject?.CreditHours ?? 0,
                SemesterId = entity.SemesterId,
                SemesterName = entity.Semester?.Name ?? string.Empty,
                DoctorId = entity.DoctorId,
                DoctorName = entity.Doctor?.FullName ?? string.Empty,
                MaxCapacity = entity.MaxCapacity,
                DepartmentId = entity.DepartmentId,
                DepartmentName = entity.Department?.Name ?? string.Empty,
                BatchId = entity.BatchId,
                BatchName = entity.Batch?.Name ?? string.Empty,
                GroupId = entity.GroupId
            };
        }
    }
}
