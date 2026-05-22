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
    public class EnrollmentService(AppDbContext context) : IEnrollmentService
    {
        private readonly AppDbContext _context = context;

        public async Task EnrollStudentAsync(CreateEnrollmentDto dto, bool skipValidation = false)
        {
            var student = await _context.Students
                .Include(s => s.Department)
                .Include(s => s.Batch)
                .Include(s => s.Group)
                .FirstOrDefaultAsync(s => s.Id == dto.StudentId);

            var offering = await _context.SubjectOfferings
                .Include(so => so.Subject)
                .Include(so => so.Department)
                .Include(so => so.Batch)
                .Include(so => so.Group)
                .FirstOrDefaultAsync(so => so.Id == dto.SubjectOfferingId);

            if (student == null) throw new KeyNotFoundException("Student not found");
            if (offering == null) throw new KeyNotFoundException("Subject Offering not found");

            if (!skipValidation)
            {
                // 1. Validate Department
                if (student.DepartmentId != offering.DepartmentId)
                    throw new InvalidOperationException($"Academic Integrity: Student (Dept: {student.Department.Name}) cannot enroll in Offering (Dept: {offering.Department.Name}).");

                // 2. Validate Batch
                if (student.BatchId != offering.BatchId)
                    throw new InvalidOperationException($"Academic Integrity: Student (Batch: {student.Batch.Name}) cannot enroll in Offering (Batch: {offering.Batch.Name}).");

                // 3. Validate Group (If offering is restricted to a group)
                if (offering.GroupId.HasValue && student.GroupId != offering.GroupId.Value)
                    throw new InvalidOperationException($"Academic Integrity: Student (Group: {student.Group.Name}) cannot enroll in Offering (Group: {offering.Group?.Name}).");
            }

            // 4. Duplicate Check
            var existing = await _context.Enrollments
                .IgnoreQueryFilters() // check soft deleted ones too
                .FirstOrDefaultAsync(e => e.StudentId == dto.StudentId && e.SubjectOfferingId == dto.SubjectOfferingId);

            if (existing != null)
            {
                if (existing.IsActive && existing.DeletedAt == null)
                    throw new InvalidOperationException("Student is already enrolled in this offering.");

                // Reactivate if soft deleted or inactive
                existing.IsActive = true;
                existing.DeletedAt = null; // Restore from soft delete
                existing.EnrolledAt = DateTime.UtcNow;

                // _context.Enrollments.Update(existing); // distinct update not needed for tracked entity
                await _context.SaveChangesAsync();
                return;
            }

            // 5. Create Enrollment
            var enrollment = new Enrollment
            {
                StudentId = dto.StudentId,
                SubjectOfferingId = dto.SubjectOfferingId,
                EnrolledAt = DateTime.UtcNow,
                IsActive = true
            };

            _context.Enrollments.Add(enrollment);
            await _context.SaveChangesAsync();
        }

        public async Task UnenrollStudentAsync(Ulid enrollmentId)
        {
            var enrollment = await _context.Enrollments.FindAsync(enrollmentId);
            if (enrollment == null) throw new KeyNotFoundException("Enrollment not found");

            // Soft Delete
            enrollment.IsActive = false;
            // BaseEntity soft delete handled by DbContext interceptor or manual setting if not using automated soft delete
            // user request says "Soft delete (set IsActive = false)". 
            // Also user mentioned "Soft Delete" in general. 
            // AppDbContext has a QueryFilter for DeletedAt. Let's set DeletedAt too to be safe/consistent with system.
            enrollment.DeletedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
        }

        public async Task<IReadOnlyList<Enrollment>> GetStudentEnrollmentsAsync(Ulid studentId)
        {
            return await _context.Enrollments
                .Include(e => e.SubjectOffering)
                .ThenInclude(so => so.Subject)
                .Include(e => e.SubjectOffering)
                .ThenInclude(so => so.Doctor)
                .Where(e => e.StudentId == studentId && e.IsActive)
                .OrderByDescending(e => e.EnrolledAt)
                .ToListAsync();
        }

        public async Task<IReadOnlyList<Enrollment>> GetEnrollmentsByOfferingIdAsync(Ulid offeringId)
        {
            return await _context.Enrollments
                .Include(e => e.Student)
                .Where(e => e.SubjectOfferingId == offeringId && e.IsActive)
                .OrderBy(e => e.Student.FullName)
                .ToListAsync();
        }
        public async Task<AutoEnrollResultDto> AutoEnrollAsync(Ulid studentId)
        {
            var student = await _context.Students
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == studentId && s.DeletedAt == null)
                ?? throw new KeyNotFoundException("Student not found.");

            // All offerings that match the student's batch + department
            // Group-restricted offerings only match if the student's group matches.
            var availableOfferings = await _context.SubjectOfferings
                .AsNoTracking()
                .Include(o => o.Subject)
                .Where(o =>
                    o.DepartmentId == student.DepartmentId &&
                    o.BatchId      == student.BatchId &&
                    (o.GroupId == null || o.GroupId == student.GroupId))
                .ToListAsync();

            if (availableOfferings.Count == 0)
                return new AutoEnrollResultDto { TotalAvailable = 0 };

            var offeringIds = availableOfferings.Select(o => o.Id).ToList();

            // Load existing enrollments for this student (including soft-deleted)
            var existing = await _context.Enrollments
                .IgnoreQueryFilters()
                .Where(e => e.StudentId == studentId && offeringIds.Contains(e.SubjectOfferingId))
                .ToListAsync();

            var activeSet    = existing.Where(e => e.IsActive && e.DeletedAt == null)
                                       .Select(e => e.SubjectOfferingId).ToHashSet();
            var inactiveDict = existing.Where(e => !e.IsActive || e.DeletedAt != null)
                                       .ToDictionary(e => e.SubjectOfferingId);

            var enrolledSubjects = new List<string>();
            var errors           = new List<string>();
            int alreadyHad       = 0;
            int enrolled         = 0;

            foreach (var offering in availableOfferings)
            {
                if (activeSet.Contains(offering.Id))
                {
                    alreadyHad++;
                    continue;
                }

                if (inactiveDict.TryGetValue(offering.Id, out var soft))
                {
                    // Reactivate soft-deleted enrollment
                    soft.IsActive  = true;
                    soft.DeletedAt = null;
                    soft.EnrolledAt = DateTime.UtcNow;
                    _context.Enrollments.Update(soft);
                    enrolledSubjects.Add(offering.Subject?.Name ?? offering.Id.ToString());
                    enrolled++;
                    continue;
                }

                try
                {
                    _context.Enrollments.Add(new Enrollment
                    {
                        StudentId         = studentId,
                        SubjectOfferingId = offering.Id,
                        EnrolledAt        = DateTime.UtcNow,
                        IsActive          = true,
                    });
                    enrolledSubjects.Add(offering.Subject?.Name ?? offering.Id.ToString());
                    enrolled++;
                }
                catch (Exception ex)
                {
                    errors.Add($"{offering.Subject?.Name}: {ex.Message}");
                }
            }

            await _context.SaveChangesAsync();

            return new AutoEnrollResultDto
            {
                Enrolled       = enrolled,
                AlreadyHad     = alreadyHad,
                Skipped        = errors.Count,
                TotalAvailable = availableOfferings.Count,
                EnrolledSubjects = enrolledSubjects,
                Errors           = errors,
            };
        }

        public async Task ReactivateEnrollmentAsync(Ulid enrollmentId)
        {
            var enrollment = await _context.Enrollments
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(e => e.Id == enrollmentId)
                ?? throw new KeyNotFoundException("Enrollment not found");

            enrollment.IsActive = true;
            enrollment.DeletedAt = null;
            await _context.SaveChangesAsync();
        }
    }
}
