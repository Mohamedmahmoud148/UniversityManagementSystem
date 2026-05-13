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

    /// <summary>PATCH DTO — every field is optional. Only non-null fields are applied.</summary>
    public record PatchStudentDto(
        string? FullName,
        string? Phone,
        string? Email,
        string? BatchCode,
        string? GroupCode,
        bool? IsActive
    );

    /// <summary>Query-param filter object for GET /api/students/filter</summary>
    public record StudentFilterDto
    {
        public Ulid? UniversityId   { get; init; }
        public Ulid? CollegeId      { get; init; }
        public Ulid? DepartmentId   { get; init; }
        public Ulid? BatchId        { get; init; }
        public Ulid? GroupId        { get; init; }
        public bool? IsActive       { get; init; }
        public string? Search       { get; init; }  // name / email / studentId
        public int Page             { get; init; } = 1;
        public int Size             { get; init; } = 20;
    };

    /// <summary>Enriched student DTO returned by the filter endpoint.</summary>
    public record StudentDetailDto
    {
        public Ulid Id { get; init; }
        public string Code { get; init; } = string.Empty;
        public string FullName { get; init; } = string.Empty;
        public string Email { get; init; } = string.Empty;
        public string UniversityEmail { get; init; } = string.Empty;
        public string Phone { get; init; } = string.Empty;
        public string NationalId { get; init; } = string.Empty;
        public string UniversityStudentId { get; init; } = string.Empty;
        public bool IsActive { get; init; }
        public Ulid UniversityId { get; init; }
        public Ulid CollegeId { get; init; }
        public string CollegeName { get; init; } = string.Empty;
        public Ulid DepartmentId { get; init; }
        public string DepartmentName { get; init; } = string.Empty;
        public Ulid BatchId { get; init; }
        public string BatchName { get; init; } = string.Empty;
        public Ulid GroupId { get; init; }
        public string GroupName { get; init; } = string.Empty;
    }

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

    /// <summary>PATCH DTO — every field is optional. Only non-null fields are applied.</summary>
    public record PatchDoctorDto(
        string? FullName,
        string? Phone,
        string? DepartmentCode
    );

    /// <summary>Query-param filter object for GET /api/doctors/filter</summary>
    public record DoctorFilterDto
    {
        public Ulid? CollegeId    { get; init; }
        public Ulid? DepartmentId { get; init; }
        public bool? IsActive     { get; init; }  // filters on SystemUser.IsActive
        public string? Search     { get; init; }  // name / email / staffId
        public int Page           { get; init; } = 1;
        public int Size           { get; init; } = 20;
    };

    /// <summary>Enriched doctor DTO returned by the filter endpoint.</summary>
    public record DoctorDetailDto
    {
        public Ulid Id { get; init; }
        public string Code { get; init; } = string.Empty;
        public string FullName { get; init; } = string.Empty;
        public string Email { get; init; } = string.Empty;
        public string UniversityEmail { get; init; } = string.Empty;
        public string Phone { get; init; } = string.Empty;
        public string UniversityStaffId { get; init; } = string.Empty;
        public Ulid DepartmentId { get; init; }
        public string DepartmentName { get; init; } = string.Empty;
        public Ulid CollegeId { get; init; }
        public string CollegeName { get; init; } = string.Empty;
    }

    /// <summary>Generic paginated response wrapper.</summary>
    public record PagedResult<T>
    {
        public IReadOnlyList<T> Data   { get; init; } = [];
        public int TotalCount          { get; init; }
        public int Page                { get; init; }
        public int Size                { get; init; }
        public int TotalPages          => (int)Math.Ceiling((double)TotalCount / Size);
        public bool HasNext            => Page < TotalPages;
        public bool HasPrev            => Page > 1;
    }

    public record SubjectDto(
        Ulid Id,
        string Name,
        string Code,
        int CreditHours,
        Ulid? CollegeId,
        string? CollegeName,
        Ulid DepartmentId,
        string DepartmentName,
        Ulid? BatchId,
        string? BatchName,
        string? DoctorName = null
    );

    public record CreateSubjectDto(
        [Required] string Name,
        [Required] string Code,
        [Required] int CreditHours,
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
        public string SubjectCode { get; init; } = string.Empty;
        public string SubjectName { get; init; } = string.Empty;
        public string DepartmentName { get; init; } = string.Empty;
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
