using System.Threading.Tasks;
using UniversityManagementSystem.Core.DTOs;

namespace UniversityManagementSystem.Core.Interfaces
{
    public interface IAuthService
    {
        Task<AuthResponseDto?> LoginAsync(UserLoginDto loginDto);
        Task<AuthResponseDto> RegisterStudentAsync(RegisterStudentDto dto, int createdByUserId);
        Task<AuthResponseDto> RegisterDoctorAsync(RegisterDoctorDto dto, int createdByUserId);
        Task<AuthResponseDto> RegisterAdminAsync(RegisterAdminDto dto, int createdByUserId);

        // New Security Methods
        Task<AuthResponseDto> RefreshTokenAsync(string token, string refreshToken);
        Task<bool> RevokeTokenAsync(string refreshToken);
        Task<bool> ChangePasswordAsync(int userId, string currentPassword, string newPassword);
    }
}
