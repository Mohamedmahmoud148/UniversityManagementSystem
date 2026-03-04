using System.Collections.Generic;
using System.Threading.Tasks;
using UniversityManagementSystem.Core.Entities;

namespace UniversityManagementSystem.Core.Interfaces
{
    public interface IStudentService
    {
        Task<IReadOnlyList<Student>> GetStudentsByBatchIdAsync(int batchId);
        Task<Student?> GetStudentByIdAsync(int id);
        Task<Student?> GetStudentByPublicIdAsync(string publicId);
        Task<Student?> GetStudentByUniversityEmailAsync(string email); // Added
        Task<Student> CreateStudentAsync(Student student);
        Task UpdateStudentAsync(Student student);
        Task UpdateStudentDetailsAsync(int id, Core.DTOs.UpdateStudentDto dto); // Added
        Task DeleteStudentAsync(int id); // Added
        Task<IReadOnlyList<Student>> GetPagedStudentsAsync(int page, int size);
        // Task ActivateStudentAsync(int id); // Could be part of Update
    }

    public interface IDoctorService
    {
        Task<IReadOnlyList<Doctor>> GetDoctorsByDepartmentIdAsync(int departmentId);
        Task<Doctor?> GetDoctorByIdAsync(int id);
        Task<Doctor?> GetDoctorByPublicIdAsync(string publicId);
        Task<Doctor?> GetDoctorByUniversityEmailAsync(string email); // Added
        Task<Doctor> CreateDoctorAsync(Doctor doctor);
        Task UpdateDoctorDetailsAsync(int id, Core.DTOs.UpdateDoctorDto dto); // Added
        Task DeleteDoctorAsync(int id); // Added
        Task<IReadOnlyList<Doctor>> GetPagedDoctorsAsync(int page, int size);
        Task AssignSubjectToDoctorAsync(int doctorId, int subjectId);
        Task<IReadOnlyList<Subject>> GetDoctorSubjectsAsync(int doctorId);
    }

    public interface ISubjectService
    {
        Task<IReadOnlyList<Subject>> GetSubjectsByBatchIdAsync(int batchId);
        Task<Subject?> GetSubjectByPublicIdAsync(string publicId);
        Task<Subject> CreateSubjectAsync(Subject subject);
        Task UpdateSubjectDetailsAsync(int id, Core.DTOs.UpdateSubjectDto dto); // Added
        Task DeleteSubjectAsync(int id); // Added
        Task AssignSubjectToDoctorAsync(int subjectId, int doctorId);
        Task AssignSubjectToAssistantAsync(int subjectId, int assistantId);
    }

}
