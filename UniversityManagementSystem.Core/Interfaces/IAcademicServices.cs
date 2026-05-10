using System.Threading.Tasks;
using UniversityManagementSystem.Core.Entities;
using NUlid;

namespace UniversityManagementSystem.Core.Interfaces
{
    public interface IStudentService
    {
        Task<IReadOnlyList<Student>> GetStudentsByBatchIdAsync(Ulid batchId);
        Task<Student?> GetStudentByIdAsync(Ulid id);
        Task<Student?> GetStudentByCodeAsync(string code);
        Task<Student?> GetStudentByUniversityEmailAsync(string email);
        Task<Student> CreateStudentAsync(Student student);
        Task UpdateStudentAsync(Student student);
        Task UpdateStudentDetailsAsync(Ulid id, Core.DTOs.UpdateStudentDto dto);
        Task DeleteStudentAsync(Ulid id);
        Task<IReadOnlyList<Student>> GetPagedStudentsAsync(int page, int size);
    }

    public interface IDoctorService
    {
        Task<IReadOnlyList<Doctor>> GetDoctorsByDepartmentIdAsync(Ulid departmentId);
        Task<Doctor?> GetDoctorByIdAsync(Ulid id);
        Task<Doctor?> GetDoctorByCodeAsync(string code);
        Task<Doctor?> GetDoctorByUniversityEmailAsync(string email);
        Task<Doctor> CreateDoctorAsync(Doctor doctor);
        Task UpdateDoctorDetailsAsync(Ulid id, Core.DTOs.UpdateDoctorDto dto);
        Task DeleteDoctorAsync(Ulid id);
        Task<IReadOnlyList<Doctor>> GetPagedDoctorsAsync(int page, int size);
        Task AssignSubjectToDoctorAsync(Ulid doctorId, Ulid subjectId);
        Task<IReadOnlyList<Subject>> GetDoctorSubjectsAsync(Ulid doctorId);
    }

    public interface ISubjectService
    {
        Task<IReadOnlyList<Subject>> GetSubjectsByBatchIdAsync(Ulid batchId);
        Task<Subject?> GetSubjectByIdAsync(Ulid id);
        Task<Subject?> GetSubjectByCodeAsync(string code);
        Task<Subject> CreateSubjectAsync(Subject subject);
        Task UpdateSubjectDetailsAsync(Ulid id, Core.DTOs.UpdateSubjectDto dto);
        Task DeleteSubjectAsync(Ulid id);
        Task AssignSubjectToDoctorAsync(Ulid subjectId, Ulid doctorId);
        Task AssignSubjectToAssistantAsync(Ulid subjectId, Ulid assistantId);
        Task<IReadOnlyList<Subject>> GetSubjectsByDepartmentIdAsync(Ulid departmentId);
        Task<IReadOnlyList<Subject>> GetSubjectsByCollegeIdAsync(Ulid collegeId);
        Task<IReadOnlyList<Subject>> GetDoctorSubjectsAsync(Ulid doctorId);
        Task<IReadOnlyList<Subject>> GetStudentSubjectsAsync(Ulid studentId);
    }

}
