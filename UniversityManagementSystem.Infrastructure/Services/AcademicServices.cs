using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UniversityManagementSystem.Core.Entities;
using UniversityManagementSystem.Core.Interfaces;
using NUlid;
using UniversityManagementSystem.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace UniversityManagementSystem.Infrastructure.Services
{
    public class StudentService(IGenericRepository<Student> repo, AppDbContext context) : IStudentService
    {
        private readonly IGenericRepository<Student> _repo = repo;
        private readonly AppDbContext _context = context;

        public async Task<IReadOnlyList<Student>> GetStudentsByBatchIdAsync(Ulid batchId)
            => await _context.Students.Include(s => s.SystemUser).Where(s => s.BatchId == batchId).ToListAsync();

        public async Task<Student?> GetStudentByIdAsync(Ulid id) => await _repo.GetByIdAsync(id);

        public async Task<Student?> GetStudentByCodeAsync(string code)
        {
            var normalizedCode = code.Trim().ToLower();
            return await _context.Students.Include(s => s.SystemUser).FirstOrDefaultAsync(s => s.Code.ToLower() == normalizedCode);
        }

        // Use SystemUser relationship for UniversityEmail
        public async Task<Student?> GetStudentByUniversityEmailAsync(string email)
        {
            return await _context.Students.Include(s => s.SystemUser).FirstOrDefaultAsync(s => s.SystemUser.UniversityEmail == email);
        }

        public async Task<Student> CreateStudentAsync(Student student) => await _repo.AddAsync(student);
        public async Task UpdateStudentAsync(Student student) => await _repo.UpdateAsync(student);
        public async Task<IReadOnlyList<Student>> GetPagedStudentsAsync(int page, int size)
            => await _context.Students.Include(s => s.SystemUser).Skip((page - 1) * size).Take(size).ToListAsync();

        public async Task UpdateStudentDetailsAsync(Ulid id, Core.DTOs.UpdateStudentDto dto)
        {
            var student = await _repo.GetByIdAsync(id);
            if (student == null) throw new Exception("Student not found");

            // Resolve BatchCode → Batch entity
            if (string.IsNullOrWhiteSpace(dto.BatchCode))
                throw new ArgumentException("BatchCode is required.");

            var batch = await _context.Batches
                .FirstOrDefaultAsync(b => b.Code.ToLower() == dto.BatchCode.ToLower())
                ?? throw new KeyNotFoundException($"Batch with code '{dto.BatchCode}' not found.");

            // Academic Integrity: new Batch must belong to same Department as student
            if (batch.Id != student.BatchId)
            {
                if (batch.DepartmentId != student.DepartmentId)
                    throw new InvalidOperationException("New Batch must belong to the Student's Department.");
            }

            // Resolve GroupCode → Group entity
            if (string.IsNullOrWhiteSpace(dto.GroupCode))
                throw new ArgumentException("GroupCode is required.");

            var group = await _context.Groups
                .FirstOrDefaultAsync(g => g.Code.ToLower() == dto.GroupCode.ToLower())
                ?? throw new KeyNotFoundException($"Group with code '{dto.GroupCode}' not found.");

            // Academic Integrity: group must belong to the resolved batch
            if (group.BatchId != batch.Id)
                throw new InvalidOperationException("Group must belong to the specified Batch.");

            student.FullName = dto.FullName;
            student.Phone = dto.Phone;
            student.BatchId = batch.Id;
            student.GroupId = group.Id;

            await _repo.UpdateAsync(student);
        }

        public async Task DeleteStudentAsync(Ulid id)
        {
            var student = await _repo.GetByIdAsync(id);
            if (student == null) throw new Exception("Student not found");

            student.DeletedAt = DateTime.UtcNow;
            await _repo.UpdateAsync(student);
        }
    }

    public class DoctorService(IGenericRepository<Doctor> repo, AppDbContext context) : IDoctorService
    {
        private readonly IGenericRepository<Doctor> _repo = repo;
        private readonly AppDbContext _context = context;

        public async Task<IReadOnlyList<Doctor>> GetDoctorsByDepartmentIdAsync(Ulid departmentId)
            => await _repo.GetAsync(d => d.DepartmentId == departmentId);

        public async Task<Doctor?> GetDoctorByIdAsync(Ulid id) => await _repo.GetByIdAsync(id);

        public async Task<Doctor?> GetDoctorByCodeAsync(string code)
        {
            var normalizedCode = code.Trim().ToLower();
            var doctors = await _repo.GetAsync(d => d.Code.ToLower() == normalizedCode);
            return doctors.FirstOrDefault();
        }

        public async Task<Doctor?> GetDoctorByUniversityEmailAsync(string email)
        {
            var doctors = await _repo.GetAsync(d => d.SystemUser.UniversityEmail == email);
            return doctors.Count > 0 ? doctors[0] : null;
        }

        public async Task<Doctor> CreateDoctorAsync(Doctor doctor) => await _repo.AddAsync(doctor);

        public async Task UpdateDoctorDetailsAsync(Ulid id, Core.DTOs.UpdateDoctorDto dto)
        {
            var doctor = await _repo.GetByIdAsync(id);
            if (doctor == null) throw new Exception("Doctor not found");

            doctor.FullName = dto.FullName;
            doctor.Phone = dto.Phone;

            await _repo.UpdateAsync(doctor);
        }

        public async Task DeleteDoctorAsync(Ulid id)
        {
            var doctor = await _repo.GetByIdAsync(id);
            if (doctor == null) throw new Exception("Doctor not found");

            doctor.DeletedAt = DateTime.UtcNow;
            await _repo.UpdateAsync(doctor);
        }

        public async Task<IReadOnlyList<Doctor>> GetPagedDoctorsAsync(int page, int size)
            => await _repo.GetPagedAsync(page, size);

        public async Task AssignSubjectToDoctorAsync(Ulid doctorId, Ulid subjectId)
        {
            var exists = await _context.SubjectDoctors.AnyAsync(sd => sd.DoctorId == doctorId && sd.SubjectId == subjectId);
            if (!exists)
            {
                _context.SubjectDoctors.Add(new SubjectDoctor { DoctorId = doctorId, SubjectId = subjectId });
                await _context.SaveChangesAsync();
            }
        }

        public async Task<IReadOnlyList<Subject>> GetDoctorSubjectsAsync(Ulid doctorId)
        {
            return await _context.SubjectDoctors
                .Where(sd => sd.DoctorId == doctorId)
                .Select(sd => sd.Subject)
                .ToListAsync();
        }
    }

    public class SubjectService(IGenericRepository<Subject> repo, AppDbContext context, ISmartStringGenerator smartString) : ISubjectService
    {
        private readonly IGenericRepository<Subject> _repo = repo;
        private readonly AppDbContext _context = context;
        private readonly ISmartStringGenerator _smartString = smartString;

        public async Task<IReadOnlyList<Subject>> GetSubjectsByBatchIdAsync(Ulid batchId)
        {
            return await _context.Subjects
                .Include(s => s.Department)
                .Include(s => s.College)
                .Include(s => s.Batch)
                .Where(s => s.BatchId == batchId)
                .ToListAsync();
        }

        public async Task<IReadOnlyList<Subject>> GetSubjectsByDepartmentIdAsync(Ulid departmentId)
        {
            return await _context.Subjects
                .Include(s => s.Department)
                .Include(s => s.College)
                .Include(s => s.Batch)
                .Where(s => s.DepartmentId == departmentId)
                .ToListAsync();
        }

        public async Task<IReadOnlyList<Subject>> GetSubjectsByCollegeIdAsync(Ulid collegeId)
        {
            return await _context.Subjects
                .Include(s => s.Department)
                .Include(s => s.College)
                .Include(s => s.Batch)
                .Where(s => s.CollegeId == collegeId)
                .ToListAsync();
        }

        public async Task<IReadOnlyList<Subject>> GetDoctorSubjectsAsync(Ulid doctorId)
        {
            return await _context.SubjectDoctors
                .Include(sd => sd.Subject)
                    .ThenInclude(s => s.Department)
                .Include(sd => sd.Subject)
                    .ThenInclude(s => s.College)
                .Include(sd => sd.Subject)
                    .ThenInclude(s => s.Batch)
                .Where(sd => sd.DoctorId == doctorId)
                .Select(sd => sd.Subject)
                .ToListAsync();
        }

        public async Task<IReadOnlyList<Subject>> GetStudentSubjectsAsync(Ulid studentId)
        {
            return await _context.Enrollments
                .Include(e => e.SubjectOffering)
                    .ThenInclude(so => so.Subject)
                        .ThenInclude(s => s.Department)
                .Include(e => e.SubjectOffering)
                    .ThenInclude(so => so.Subject)
                        .ThenInclude(s => s.College)
                .Include(e => e.SubjectOffering)
                    .ThenInclude(so => so.Subject)
                        .ThenInclude(s => s.Batch)
                .Where(e => e.StudentId == studentId && e.IsActive)
                .Select(e => e.SubjectOffering.Subject)
                .Distinct()
                .ToListAsync();
        }

        public async Task<Subject?> GetSubjectByIdAsync(Ulid id)
        {
            return await _context.Subjects.FindAsync(id);
        }

        public async Task<Subject> CreateSubjectAsync(Subject subject)
        {
            subject.Code = await _smartString.GenerateUniqueAsync<Subject>(subject.Code, s => s.Code);
            return await _repo.AddAsync(subject);
        }

        public async Task<Subject?> GetSubjectByCodeAsync(string code)
        {
            var normalizedCode = code.Trim().ToLower();
            return await _context.Subjects
                .Include(s => s.Department)
                .Include(s => s.College)
                .Include(s => s.Batch)
                .FirstOrDefaultAsync(s => s.Code.ToLower() == normalizedCode);
        }

        public async Task UpdateSubjectDetailsAsync(Ulid id, Core.DTOs.UpdateSubjectDto dto)
        {
            var subject = await _repo.GetByIdAsync(id);
            if (subject == null) throw new Exception("Subject not found");

            subject.Name = dto.Name;
            subject.Code = dto.Code;

            await _repo.UpdateAsync(subject);
        }

        public async Task DeleteSubjectAsync(Ulid id)
        {
            // Validation: Check for active AttendanceSessions
            var hasActiveSessions = await _context.AttendanceSessions.AnyAsync(s => s.SubjectId == id && s.IsActive);
            if (hasActiveSessions) throw new Exception("Cannot delete subject with active attendance sessions.");

            // Validation: Check for SubjectOfferings
            var hasOfferings = await _context.SubjectOfferings.AnyAsync(so => so.SubjectId == id);
            if (hasOfferings) throw new Exception("Cannot delete subject with existing offerings.");

            var subject = await _repo.GetByIdAsync(id);
            if (subject == null) throw new Exception("Subject not found");

            subject.DeletedAt = DateTime.UtcNow;
            await _repo.UpdateAsync(subject);
        }

        public async Task AssignSubjectToDoctorAsync(Ulid subjectId, Ulid doctorId)
        {
            var subject = await _context.Subjects.FindAsync(subjectId);
            var doctor = await _context.Doctors.FindAsync(doctorId);

            if (subject == null) throw new Exception("Subject not found");
            if (doctor == null) throw new Exception("Doctor not found");

            if (subject.DepartmentId != doctor.DepartmentId)
                throw new Exception("Academic Integrity Violation: Doctor and Subject must belong to the same Department.");

            var exists = await _context.SubjectDoctors.AnyAsync(sd => sd.SubjectId == subjectId && sd.DoctorId == doctorId);
            if (!exists)
            {
                _context.SubjectDoctors.Add(new SubjectDoctor { SubjectId = subjectId, DoctorId = doctorId });
                await _context.SaveChangesAsync();
            }
        }

        public async Task AssignSubjectToAssistantAsync(Ulid subjectId, Ulid assistantId)
        {
            var exists = await _context.SubjectAssistants.AnyAsync(sa => sa.SubjectId == subjectId && sa.TeachingAssistantId == assistantId);
            if (!exists)
            {
                _context.SubjectAssistants.Add(new SubjectAssistant { SubjectId = subjectId, TeachingAssistantId = assistantId });
                await _context.SaveChangesAsync();
            }
        }
    }

}
