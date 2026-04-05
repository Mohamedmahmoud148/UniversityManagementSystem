using System;
using System.ComponentModel.DataAnnotations;
using NUlid;

namespace UniversityManagementSystem.Core.DTOs
{
    // ── Standard: ALL responses include id + code + name ──────────────────────

    public record UniversityDto(Ulid Id, string Name, string Code);
    public record CreateUniversityDto([Required] string Name, [Required] string Code);

    public record CollegeDto(Ulid Id, string Name, string Code, Ulid UniversityId);
    /// <summary>
    /// Admin creates a College by referencing the University by its public Code,
    /// not by internal ULID. Controller resolves UniversityCode → UniversityId.
    /// </summary>
    public record CreateCollegeDto(
        [Required] string Name,
        [Required] string Code,
        [Required] string UniversityCode   // Admin input: public code, not internal ULID
    );

    public record DepartmentDto(Ulid Id, string Name, string Code, Ulid CollegeId);
    public record CreateDepartmentDto(
        [Required] string Name,
        [Required] string Code,            // Missing before
        [Required] string CollegeCode      // Admin input: public code, not internal ULID
    );

    public record BatchDto(Ulid Id, string Name, string Code, Ulid DepartmentId);
    public record CreateBatchDto(
        [Required] string Name,
        [Required] string Code,            // Missing before
        [Required] string DepartmentCode   // Admin input: public code, not internal ULID
    );
}

