using System;
using System.ComponentModel.DataAnnotations;
using NUlid;

namespace UniversityManagementSystem.Core.DTOs
{
    public record AdminDto(
        Ulid Id,
        string FullName,
        string Email,
        string Phone,
        string Role,
        bool IsActive,
        DateTime CreatedAt
    );

    public record UpdateAdminDto(
        [Required] string FullName,
        string? Phone
    );
}
