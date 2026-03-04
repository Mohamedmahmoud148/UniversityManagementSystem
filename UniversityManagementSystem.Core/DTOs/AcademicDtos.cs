using System;
using System.ComponentModel.DataAnnotations;

namespace UniversityManagementSystem.Core.DTOs
{
    public record StudentDto
    {
        public int Id { get; init; }
        public string PublicId { get; init; } = string.Empty;
        public string FullName { get; init; } = string.Empty;
        public string Email { get; init; } = string.Empty;
        public string Phone { get; init; } = string.Empty;
        public string NationalId { get; init; } = string.Empty;
        public string UniversityStudentId { get; init; } = string.Empty;
        public string UniversityEmail { get; init; } = string.Empty;
        public int UniversityId { get; init; }
        public int BatchId { get; init; }
        public int GroupId { get; init; }
        public bool IsActive { get; init; }
    }

    public record CreateStudentDto(
        [Required] string FullName,
        [Required] string NationalId,
        [Required][Phone] string Phone,
        [Required] int BatchId,
        [Required] int GroupId
    );

    public record UpdateStudentDto(
        [Required] string FullName,
        [Required][Phone] string Phone,
        [Required] int BatchId,
        [Required] int GroupId
    );

    public record DoctorDto
    {
        public int Id { get; init; }
        public string PublicId { get; init; } = string.Empty;
        public string FullName { get; init; } = string.Empty;
        public string Email { get; init; } = string.Empty;
        public string Phone { get; init; } = string.Empty;
        public string UniversityStaffId { get; init; } = string.Empty;
        public string UniversityEmail { get; init; } = string.Empty;
        public int DepartmentId { get; init; }
    }

    public record CreateDoctorDto(
        [Required] string FullName,
        [Required] string NationalId,
        [Required][Phone] string Phone,
        [Required] int DepartmentId
    );

    public record UpdateDoctorDto(
        [Required] string FullName,
        [Required][Phone] string Phone
    );

    public record SubjectDto(int Id, string Name, string Code, int? CollegeId, int DepartmentId, int? BatchId);

    public record CreateSubjectDto(
        [Required] string Name,
        [Required] string Code,
        int? CollegeId,
        [Required] int DepartmentId,
        int? BatchId
    );

    public record UpdateSubjectDto(
        [Required] string Name,
        [Required] string Code
    );

    public record EnrollmentDto
    {
        public int Id { get; init; }
        public int StudentId { get; init; }
        public string StudentName { get; init; } = string.Empty;
        public int SubjectOfferingId { get; init; }
        public string SubjectName { get; init; } = string.Empty;
        public string DoctorName { get; init; } = string.Empty;
        public string SemesterName { get; init; } = string.Empty;
        public DateTime EnrolledAt { get; init; }
        public bool IsActive { get; init; } // Should match IsActive logic
    }

    public record CreateEnrollmentDto(
        [Required] int StudentId,
        [Required] int SubjectOfferingId
    );

    public record GroupDto(int Id, string Name, int BatchId);

    public record CreateGroupDto(
        [Required] string Name,
        [Required] int BatchId
    );
}
