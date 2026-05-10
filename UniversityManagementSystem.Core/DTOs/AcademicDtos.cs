using System;
using System.ComponentModel.DataAnnotations;
using NUlid;

namespace UniversityManagementSystem.Core.DTOs
{
    public record StudentDto
    {
        public Ulid Id { get; init; }
        public string Code { get; init; } = string.Empty;
        public string FullName { get; init; } = string.Empty;
        public string Email { get; init; } = string.Empty;
        public string Phone { get; init; } = string.Empty;
        public string NationalId { get; init; } = string.Empty;
        public string UniversityStudentId { get; init; } = string.Empty;
        public string UniversityEmail { get; init; } = string.Empty;
        public Ulid UniversityId { get; init; }
        public Ulid BatchId { get; init; }
        public Ulid GroupId { get; init; }
        public bool IsActive { get; init; }
    }

    public record CreateStudentDto(
        [Required] string FullName,
        [Required] string NationalId,
        [Required][Phone] string Phone,
        [Required] string BatchCode,
        [Required] string GroupCode,
        string? CollegeCode,
        string? DepartmentCode,
        string? UniversityStudentId
    );

    public record UpdateStudentDto(
        [Required] string FullName,
        [Required][Phone] string Phone,
        [Required] string BatchCode,     // replaces BatchId
        [Required] string GroupCode      // replaces GroupId
    );

    public record DoctorDto
    {
        public Ulid Id { get; init; }
        public string Code { get; init; } = string.Empty;
        public string FullName { get; init; } = string.Empty;
        public string Email { get; init; } = string.Empty;
        public string Phone { get; init; } = string.Empty;
        public string UniversityStaffId { get; init; } = string.Empty;
        public string UniversityEmail { get; init; } = string.Empty;
        public Ulid DepartmentId { get; init; }
    }

    public record CreateDoctorDto(
        [Required] string FullName,
        [Required] string NationalId,
        [Required][Phone] string Phone,
        [Required] string DepartmentCode  // replaces DepartmentId
    );

    public record UpdateDoctorDto(
        [Required] string FullName,
        [Required][Phone] string Phone
    );

    public record SubjectDto(Ulid Id, string Name, string Code, Ulid? CollegeId, Ulid DepartmentId, Ulid? BatchId);

    public record CreateSubjectDto(
        [Required] string Name,
        [Required] string Code,
        string? CollegeCode,             // replaces CollegeId  (optional)
        [Required] string DepartmentCode, // replaces DepartmentId
        string? BatchCode                // replaces BatchId    (optional)
    );

    public record UpdateSubjectDto(
        [Required] string Name,
        [Required] string Code
    );

    /// <summary>Lightweight DTO returned by GET /api/subjects/search.</summary>
    public record SubjectSearchDto(string Id, string Name, string Code);

    public record EnrollmentDto
    {
        public Ulid Id { get; init; }
        public Ulid StudentId { get; init; }
        public string StudentName { get; init; } = string.Empty;
        public Ulid SubjectOfferingId { get; init; }
        public string SubjectName { get; init; } = string.Empty;
        public string DoctorName { get; init; } = string.Empty;
        public string SemesterName { get; init; } = string.Empty;
        public DateTime EnrolledAt { get; init; }
        public bool IsActive { get; init; }
    }

    public record CreateEnrollmentDto(
        [Required] Ulid StudentId,
        [Required] Ulid SubjectOfferingId
    );

    /// <summary>GroupDto: id+code+name standard — Code field added for AI/API consistency.</summary>
    public record GroupDto(Ulid Id, string Name, string Code, Ulid BatchId);

    public record CreateGroupDto(
        [Required] string Name,
        [Required] string Code,           // Missing before
        [Required] string BatchCode       // replaces BatchId
    );
}
