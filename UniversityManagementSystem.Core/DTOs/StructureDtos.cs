using System;
using System.ComponentModel.DataAnnotations;
using NUlid;

namespace UniversityManagementSystem.Core.DTOs
{
    public record UniversityDto(Ulid Id, string Name);
    public record CreateUniversityDto([Required] string Name);

    public record CollegeDto(Ulid Id, string Name, string Code, Ulid UniversityId);
    public record CreateCollegeDto([Required] string Name, [Required] Ulid UniversityId);

    public record DepartmentDto(Ulid Id, string Name, string Code, Ulid CollegeId);
    public record CreateDepartmentDto(
        [Required] string Name,
        [Required] string CollegeCode     // replaces CollegeId
    );

    public record BatchDto(Ulid Id, string Name, string Code, Ulid DepartmentId);
    public record CreateBatchDto(
        [Required] string Name,
        [Required] string DepartmentCode  // replaces DepartmentId
    );
}
