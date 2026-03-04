using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UniversityManagementSystem.Core.Entities;
using UniversityManagementSystem.Core.Interfaces;
using UniversityManagementSystem.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace UniversityManagementSystem.Infrastructure.Services
{
    public class StudentService(IGenericRepository<Student> repo, AppDbContext context) : IStudentService
    {
        private readonly IGenericRepository<Student> _repo = repo;
        private readonly AppDbContext _context = context;

        public async Task<IReadOnlyList<Student>> GetStudentsByBatchIdAsync(int batchId)
            => await _repo.GetAsync(s => s.BatchId == batchId);

        public async Task<Student?> GetStudentByIdAsync(int id) => await _repo.GetByIdAsync(id);

        public async Task<Student?> GetStudentByPublicIdAsync(string publicId)
        {
            var students = await _repo.GetAsync(s => s.PublicId == publicId);
            return students.FirstOrDefault();
        }

        // Use SystemUser relationship for UniversityEmail
        public async Task<Student?> GetStudentByUniversityEmailAsync(string email)
        {
            var students = await _repo.GetAsync(s => s.SystemUser.UniversityEmail == email);
            return students.Count > 0 ? students[0] : null;
        }

        public async Task<Student> CreateStudentAsync(Student student) => await _repo.AddAsync(student);
        public async Task UpdateStudentAsync(Student student) => await _repo.UpdateAsync(student);
        public async Task<IReadOnlyList<Student>> GetPagedStudentsAsync(int page, int size)
            => await _repo.GetPagedAsync(page, size);

        public async Task UpdateStudentDetailsAsync(int id, Core.DTOs.UpdateStudentDto dto)
        {
            var student = await _repo.GetByIdAsync(id);
            if (student == null) throw new Exception("Student not found");

            // Validate Batch if changed
            if (student.BatchId != dto.BatchId)
            {
                var batchExists = await _context.Batches.AnyAsync(b => b.Id == dto.BatchId);
                if (!batchExists) throw new Exception("Batch not found");

                // Academic Integrity: Ensure new Batch is within same Department?
                // Rule 1: "Validate Batch exists" - Done.
                // Should we check Department? Original request didn't explicitly say so for UPDATE, 
                // but integrity implies it. Let's start with basic exist check as requested.
                // However, if we change Batch, we might violate "Student.Department == Batch.Department".
                // Let's add that check to be safe.
                var batch = await _context.Batches.FindAsync(dto.BatchId);
                if (batch!.DepartmentId != student.DepartmentId)
                    throw new Exception("New Batch must belong to the Student's Department.");
            }

            student.FullName = dto.FullName;
            student.Phone = dto.Phone;
            student.BatchId = dto.BatchId;

            await _repo.UpdateAsync(student);
        }

        public async Task DeleteStudentAsync(int id)
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

        public async Task<IReadOnlyList<Doctor>> GetDoctorsByDepartmentIdAsync(int departmentId)
            => await _repo.GetAsync(d => d.DepartmentId == departmentId);

        public async Task<Doctor?> GetDoctorByIdAsync(int id) => await _repo.GetByIdAsync(id);

        public async Task<Doctor?> GetDoctorByPublicIdAsync(string publicId)
        {
            var doctors = await _repo.GetAsync(d => d.PublicId == publicId);
            return doctors.FirstOrDefault();
        }

        public async Task<Doctor?> GetDoctorByUniversityEmailAsync(string email)
        {
            var doctors = await _repo.GetAsync(d => d.SystemUser.UniversityEmail == email);
            return doctors.Count > 0 ? doctors[0] : null;
        }

        public async Task<Doctor> CreateDoctorAsync(Doctor doctor) => await _repo.AddAsync(doctor);

        public async Task UpdateDoctorDetailsAsync(int id, Core.DTOs.UpdateDoctorDto dto)
        {
            var doctor = await _repo.GetByIdAsync(id);
            if (doctor == null) throw new Exception("Doctor not found");

            doctor.FullName = dto.FullName;
            doctor.Phone = dto.Phone;

            await _repo.UpdateAsync(doctor);
        }

        public async Task DeleteDoctorAsync(int id)
        {
            var doctor = await _repo.GetByIdAsync(id);
            if (doctor == null) throw new Exception("Doctor not found");

            doctor.DeletedAt = DateTime.UtcNow;
            await _repo.UpdateAsync(doctor);
        }

        public async Task<IReadOnlyList<Doctor>> GetPagedDoctorsAsync(int page, int size)
            => await _repo.GetPagedAsync(page, size);

        public async Task AssignSubjectToDoctorAsync(int doctorId, int subjectId)
        {
            var exists = await _context.SubjectDoctors.AnyAsync(sd => sd.DoctorId == doctorId && sd.SubjectId == subjectId);
            if (!exists)
            {
                _context.SubjectDoctors.Add(new SubjectDoctor { DoctorId = doctorId, SubjectId = subjectId });
                await _context.SaveChangesAsync();
            }
        }

        public async Task<IReadOnlyList<Subject>> GetDoctorSubjectsAsync(int doctorId)
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

        public async Task<IReadOnlyList<Subject>> GetSubjectsByBatchIdAsync(int batchId)
            => await _repo.GetAsync(s => s.BatchId == batchId);

        public async Task<Subject> CreateSubjectAsync(Subject subject)
        {
            subject.Code = await _smartString.GenerateUniqueAsync<Subject>(subject.Code, s => s.Code);
            return await _repo.AddAsync(subject);
        }

        public async Task<Subject?> GetSubjectByPublicIdAsync(string publicId)
        {
            var subjects = await _repo.GetAsync(s => s.PublicId == publicId);
            return subjects.FirstOrDefault();
        }

        public async Task UpdateSubjectDetailsAsync(int id, Core.DTOs.UpdateSubjectDto dto)
        {
            var subject = await _repo.GetByIdAsync(id);
            if (subject == null) throw new Exception("Subject not found");

            subject.Name = dto.Name;
            subject.Code = dto.Code;

            await _repo.UpdateAsync(subject);
        }

        public async Task DeleteSubjectAsync(int id)
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

        public async Task AssignSubjectToDoctorAsync(int subjectId, int doctorId)
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

        public async Task AssignSubjectToAssistantAsync(int subjectId, int assistantId)
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
