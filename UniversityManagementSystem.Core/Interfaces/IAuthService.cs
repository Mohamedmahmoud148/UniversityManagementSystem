using System.Threading.Tasks;
using UniversityManagementSystem.Core.DTOs;
using NUlid;

namespace UniversityManagementSystem.Core.Interfaces
{
    public interface IAuthService
    {
        Task<AuthResponseDto?> LoginAsync(UserLoginDto loginDto);
        Task<AuthResponseDto> RegisterStudentAsync(RegisterStudentDto dto, Ulid createdByUserId);
        Task<AuthResponseDto> RegisterDoctorAsync(RegisterDoctorDto dto, Ulid createdByUserId);
        Task<AuthResponseDto> RegisterAdminAsync(RegisterAdminDto dto, Ulid createdByUserId);

        // New Security Methods
        Task<AuthResponseDto> RefreshTokenAsync(string token, string refreshToken);
        Task<bool> RevokeTokenAsync(string refreshToken);
        Task<bool> ChangePasswordAsync(Ulid userId, string currentPassword, string newPassword);

        /// <summary>Admin-only: reset any user's password without knowing the current one.</summary>
        Task<string> ResetPasswordAsync(Ulid targetUserId);
    }
}
