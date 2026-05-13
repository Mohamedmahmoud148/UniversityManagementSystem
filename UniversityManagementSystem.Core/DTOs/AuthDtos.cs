using System.ComponentModel.DataAnnotations;
using UniversityManagementSystem.Core.Entities;
using NUlid;

namespace UniversityManagementSystem.Core.DTOs
{
    public class UserRegisterDto
    {
        [Required]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        [MinLength(6)]
        public string Password { get; set; } = string.Empty;

        [Required]
        public UserRole Role { get; set; }
    }

    public class UserLoginDto
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string Password { get; set; } = string.Empty;
    }

    public class RegisterSystemUserDto
    {
        public string Sample { get; set; } = string.Empty;
    }

    public class RegisterAdminDto
    {
        [Required]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [Phone]
        public string Phone { get; set; } = string.Empty;

        [Required]
        public string NationalId { get; set; } = string.Empty;
    }

    public class RegisterDoctorDto
    {
        [Required]
        public string FullName { get; set; } = string.Empty;

        public string? UniversityStaffId { get; set; }

        [Required]
        public string DepartmentCode { get; set; } = string.Empty;  // replaces DepartmentId

        [Required]
        public string NationalId { get; set; } = string.Empty;

        [Required]
        [Phone]
        public string Phone { get; set; } = string.Empty;
    }

    public class RegisterStudentDto
    {
        [Required]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string CollegeCode { get; set; } = string.Empty;  // replaces CollegeId

        [Required]
        public string DepartmentCode { get; set; } = string.Empty;  // replaces DepartmentId

        [Required]
        public string NationalId { get; set; } = string.Empty;

        public string? UniversityStudentId { get; set; }

        [Required]
        public string BatchCode { get; set; } = string.Empty;  // replaces BatchId

        [Required]
        public string GroupCode { get; set; } = string.Empty;  // replaces GroupId

        [Required]
        [Phone]
        public string Phone { get; set; } = string.Empty;
    }

    public class AuthResponseDto
    {
        public string Token { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public Ulid UserId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public string? UniversityEmail { get; set; }
        public string? GeneratedUniversityId { get; set; }
        public string? TemporaryPassword { get; set; }
        public string? GeneratedPassword { get; set; }
        public bool RequiresPasswordChange { get; set; } = false;
    }

    public class RefreshTokenRequestDto
    {
        [Required]
        public string Token { get; set; } = string.Empty;

        [Required]
        public string RefreshToken { get; set; } = string.Empty;
    }

    public class RevokeTokenRequestDto
    {
        [Required]
        public string RefreshToken { get; set; } = string.Empty;
    }

    public class ChangePasswordDto
    {
        [Required]
        public string CurrentPassword { get; set; } = string.Empty;

        [Required]
        [MinLength(6)]
        public string NewPassword { get; set; } = string.Empty;
    }
}
