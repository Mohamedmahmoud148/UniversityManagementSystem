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
    public class EnrollmentService(AppDbContext context) : IEnrollmentService
    {
        private readonly AppDbContext _context = context;

        public async Task EnrollStudentAsync(CreateEnrollmentDto dto)
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

            // 1. Validate Department
            if (student.DepartmentId != offering.DepartmentId)
                throw new InvalidOperationException($"Academic Integrity: Student (Dept: {student.Department.Name}) cannot enroll in Offering (Dept: {offering.Department.Name}).");

            // 2. Validate Batch
            if (student.BatchId != offering.BatchId)
                throw new InvalidOperationException($"Academic Integrity: Student (Batch: {student.Batch.Name}) cannot enroll in Offering (Batch: {offering.Batch.Name}).");

            // 3. Validate Group (If offering is restricted to a group)
            if (offering.GroupId.HasValue)
            {
                if (student.GroupId != offering.GroupId.Value)
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

        public async Task UnenrollStudentAsync(int enrollmentId)
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

        public async Task<IReadOnlyList<Enrollment>> GetStudentEnrollmentsAsync(int studentId)
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

        public async Task<IReadOnlyList<Enrollment>> GetEnrollmentsByOfferingIdAsync(int offeringId)
        {
            return await _context.Enrollments
                .Include(e => e.Student)
                .Where(e => e.SubjectOfferingId == offeringId && e.IsActive)
                .OrderBy(e => e.Student.FullName)
                .ToListAsync();
        }
        public async Task ReactivateEnrollmentAsync(int enrollmentId)
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
