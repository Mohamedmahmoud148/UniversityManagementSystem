using System;
using System.ComponentModel.DataAnnotations;
using NUlid;

namespace UniversityManagementSystem.Core.DTOs
{
    // ── Standard: ALL responses include id + code + name ──────────────────────

    public record UniversityDto(Ulid Id, string Name, string Code);
    public record CreateUniversityDto([Required] string Name, [Required] string Code);

    public record CollegeDto(Ulid Id, string Name, string Code, Ulid UniversityId);
    public record CreateCollegeDto(
        [Required] string Name,
        [Required] string Code,
        [Required] string UniversityId     // ULID of the parent University
    );

    public record DepartmentDto(Ulid Id, string Name, string Code, Ulid CollegeId);
    public record CreateDepartmentDto(
        [Required] string Name,
        [Required] string Code,
        [Required] string CollegeId,       // ULID of the parent College
        string? AcademicYearId = null
    );

    public record BatchDto(Ulid Id, string Name, string Code, Ulid DepartmentId);
    public record CreateBatchDto(
        [Required] string Name,
        [Required] string Code,
        [Required] string DepartmentId     // ULID of the parent Department
    );
}

