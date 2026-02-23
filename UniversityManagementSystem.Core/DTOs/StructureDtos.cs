using System;
using System.ComponentModel.DataAnnotations;

namespace UniversityManagementSystem.Core.DTOs
{
    public record UniversityDto(int Id, string Name);
    public record CreateUniversityDto([Required] string Name);

    public record CollegeDto(int Id, string Name, int UniversityId);
    public record CreateCollegeDto([Required] string Name, [Required] int UniversityId);

    public record DepartmentDto(int Id, string Name, int CollegeId);
    public record CreateDepartmentDto([Required] string Name, [Required] int CollegeId);

    public record BatchDto(int Id, string Name, int DepartmentId);
    public record CreateBatchDto([Required] string Name, [Required] int DepartmentId);
}
