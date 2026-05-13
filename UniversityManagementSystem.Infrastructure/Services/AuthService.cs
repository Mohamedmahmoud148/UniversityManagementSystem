using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using UniversityManagementSystem.Core.DTOs;
using UniversityManagementSystem.Core.Entities;
using UniversityManagementSystem.Core.Interfaces;
using UniversityManagementSystem.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using NUlid;

namespace UniversityManagementSystem.Infrastructure.Services
{
    public class AuthService : IAuthService
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly string _jwtSecret;
        private readonly string _jwtIssuer;
        private readonly string _jwtAudience;
        private readonly double _jwtExpirationHours;

        public AuthService(AppDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;

            _jwtSecret = Environment.GetEnvironmentVariable("JWT_SECRET")
                         ?? configuration["JwtSettings:SecretKey"]
                         ?? throw new InvalidOperationException("JWT_SECRET environment variable is missing.");

            _jwtIssuer = _configuration["JwtSettings:Issuer"] ?? "UniversityApp";
            _jwtAudience = _configuration["JwtSettings:Audience"] ?? "UniversityAppUser";
            _jwtExpirationHours = double.TryParse(_configuration["JwtSettings:ExpirationInHours"], out var h) ? h : 1;
        }

        public async Task<AuthResponseDto?> LoginAsync(UserLoginDto loginDto)
        {
            // Login supports both personal Email and UniversityEmail
            // Students are created with UniversityEmail auto-generated; their Email field
            // may be empty or a personal email — so we search both columns.
            var emailTrimmed = loginDto.Email.Trim();
            var user = await _context.SystemUsers.FirstOrDefaultAsync(u =>
                u.Email == emailTrimmed || u.UniversityEmail == emailTrimmed);

            if (user == null) return null;


            // Check Lockout
            if (user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTime.UtcNow)
                throw new Exception($"Account locked until {user.LockoutEnd.Value}");

            if (!BCrypt.Net.BCrypt.Verify(loginDto.Password.Trim(), user.PasswordHash))
            {
                user.AccessFailedCount++;
                if (user.AccessFailedCount >= 5)
                {
                    user.LockoutEnd = DateTime.UtcNow.AddMinutes(15);
                    user.AccessFailedCount = 0;
                }
                await _context.SaveChangesAsync();
                return null;
            }

            // Reset lockout on success
            if (user.AccessFailedCount > 0 || user.LockoutEnd.HasValue)
            {
                user.AccessFailedCount = 0;
                user.LockoutEnd = null;
                await _context.SaveChangesAsync();
            }

            if (!user.IsActive) throw new Exception("Account is inactive.");

            var response = await GenerateAuthResponseAsync(user);
            response.RequiresPasswordChange = user.MustChangePassword;
            return response;
        }

        public async Task<AuthResponseDto> RefreshTokenAsync(string token, string refreshToken)
        {
            var validatedToken = GetPrincipalFromToken(token) ?? throw new Exception("Invalid Token");

            var expiryDateUnix = long.Parse(validatedToken.Claims.Single(x => x.Type == JwtRegisteredClaimNames.Exp).Value);
            var expiryDateTimeUtc = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(expiryDateUnix);

            if (expiryDateTimeUtc > DateTime.UtcNow)
                throw new Exception("Token has not expired yet");

            var jti = validatedToken.Claims.Single(x => x.Type == JwtRegisteredClaimNames.Jti).Value;
            var storedRefreshToken = await _context.RefreshTokens.FirstOrDefaultAsync(x => x.Token == refreshToken);

            if (storedRefreshToken == null ||
                storedRefreshToken.ExpiryDate < DateTime.UtcNow ||
                storedRefreshToken.Invalidated ||
                storedRefreshToken.Used ||
                storedRefreshToken.JwtId != jti)
            {
                throw new Exception("Invalid Refresh Token");
            }

            storedRefreshToken.Used = true;
            _context.RefreshTokens.Update(storedRefreshToken);
            await _context.SaveChangesAsync();

            var user = await _context.SystemUsers.FindAsync(storedRefreshToken.UserId);
            return await GenerateAuthResponseAsync(user!);
        }

        public async Task<bool> RevokeTokenAsync(string refreshToken)
        {
            var storedToken = await _context.RefreshTokens.FirstOrDefaultAsync(x => x.Token == refreshToken);
            if (storedToken == null) return false;

            storedToken.Invalidated = true;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> ChangePasswordAsync(Ulid userId, string currentPassword, string newPassword)
        {
            var user = await _context.SystemUsers.FindAsync(userId);
            if (user == null) return false;

            if (!BCrypt.Net.BCrypt.Verify(currentPassword, user.PasswordHash)) return false;

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
            user.MustChangePassword = false;
            await _context.SaveChangesAsync();
            return true;
        }

        // ── Phone normalization ────────────────────────────────────────────────
        private static string NormalizePhone(string phone)
        {
            phone = phone?.Trim() ?? "";
            // Strip +20 or 0020 country code → keep local 01xxxxxxxxx format
            if (phone.StartsWith("+20")) phone = "0" + phone[3..];
            else if (phone.StartsWith("0020")) phone = "0" + phone[4..];
            // Remove spaces/dashes
            phone = phone.Replace(" ", "").Replace("-", "");
            // If empty after normalization, use placeholder
            return string.IsNullOrEmpty(phone) ? "01000000000" : phone;
        }

        public async Task<AuthResponseDto> RegisterStudentAsync(RegisterStudentDto dto, Ulid createdByUserId)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Auto-generate credentials
                string password = GeneratePassword();
                string universityEmail = await GenerateUniversityEmailAsync("student", UserRole.Student);
                string universityIdStr = await GenerateUniversityIdAsync(UserRole.Student);

                // Ensure uniqueness checks
                if (await _context.SystemUsers.AnyAsync(u => u.NationalId == dto.NationalId))
                    throw new InvalidOperationException("NationalId already exists.");

                // Resolve CollegeCode → College
                if (string.IsNullOrWhiteSpace(dto.CollegeCode))
                    throw new ArgumentException("CollegeCode is required.");
                var college = await _context.Colleges
                    .FirstOrDefaultAsync(c => c.Code.ToLower() == dto.CollegeCode.ToLower())
                    ?? throw new KeyNotFoundException($"College with code '{dto.CollegeCode}' not found.");

                // Resolve DepartmentCode → Department
                if (string.IsNullOrWhiteSpace(dto.DepartmentCode))
                    throw new ArgumentException("DepartmentCode is required.");
                var department = await _context.Departments
                    .FirstOrDefaultAsync(d => d.Code.ToLower() == dto.DepartmentCode.ToLower())
                    ?? throw new KeyNotFoundException($"Department with code '{dto.DepartmentCode}' not found.");

                // Academic Integrity: Department must belong to College
                if (department.CollegeId != college.Id)
                    throw new InvalidOperationException("Department does not belong to the selected College.");

                // Resolve BatchCode → Batch
                if (string.IsNullOrWhiteSpace(dto.BatchCode))
                    throw new ArgumentException("BatchCode is required.");
                var batch = await _context.Batches
                    .FirstOrDefaultAsync(b => b.Code.ToLower() == dto.BatchCode.ToLower())
                    ?? throw new KeyNotFoundException($"Batch with code '{dto.BatchCode}' not found.");

                // Academic Integrity: Batch must belong to Department
                if (batch.DepartmentId != department.Id)
                    throw new InvalidOperationException("Batch does not belong to the selected Department.");

                // Resolve GroupCode → Group
                if (string.IsNullOrWhiteSpace(dto.GroupCode))
                    throw new ArgumentException("GroupCode is required.");
                var group = await _context.Groups
                    .FirstOrDefaultAsync(g => g.Code.ToLower() == dto.GroupCode.ToLower())
                    ?? throw new KeyNotFoundException($"Group with code '{dto.GroupCode}' not found.");

                // Academic Integrity: Group must belong to Batch
                if (group.BatchId != batch.Id)
                    throw new InvalidOperationException("Group does not belong to the selected Batch.");

                var universityId = college.UniversityId;

                var user = new SystemUser
                {
                    FullName = dto.FullName,
                    UniversityEmail = universityEmail,
                    Email = dto.Email,
                    NationalId = dto.NationalId,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                    Role = UserRole.Student,
                    CreatedByUserId = createdByUserId,
                    MustChangePassword = true
                };

                _context.SystemUsers.Add(user);
                await _context.SaveChangesAsync();

                var student = new Student
                {
                    FullName = dto.FullName,
                    Code = universityIdStr, // Map the code correctly so it's not empty
                    Email = dto.Email, // Map personal email
                    Phone = NormalizePhone(dto.Phone),   // handles +201... or 01...
                    UniversityStudentId = universityIdStr,
                    UniversityId = universityId,
                    CollegeId = college.Id,
                    DepartmentId = department.Id,
                    BatchId = batch.Id,
                    GroupId = group.Id,
                    SystemUserId = user.Id,
                    RegulationId = batch.RegulationId   // inherit from batch automatically
                };

                _context.Students.Add(student);
                await _context.SaveChangesAsync();

                await transaction.CommitAsync();

                var response = await GenerateAuthResponseAsync(user);
                response.UniversityEmail = universityEmail;
                response.GeneratedUniversityId = universityIdStr;
                response.TemporaryPassword = password;

                return response;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<AuthResponseDto> RegisterDoctorAsync(RegisterDoctorDto dto, Ulid createdByUserId)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                string password = GeneratePassword();
                string universityEmail = await GenerateUniversityEmailAsync("doctor", UserRole.Doctor);
                string universityId = await GenerateUniversityIdAsync(UserRole.Doctor);

                if (await _context.SystemUsers.AnyAsync(u => u.NationalId == dto.NationalId))
                    throw new InvalidOperationException("NationalId already exists.");

                // Resolve DepartmentCode → Department
                if (string.IsNullOrWhiteSpace(dto.DepartmentCode))
                    throw new ArgumentException("DepartmentCode is required.");
                var department = await _context.Departments
                    .FirstOrDefaultAsync(d => d.Code.ToLower() == dto.DepartmentCode.ToLower())
                    ?? throw new KeyNotFoundException($"Department with code '{dto.DepartmentCode}' not found.");

                var user = new SystemUser
                {
                    FullName = dto.FullName,
                    UniversityEmail = universityEmail,
                    Email = universityEmail,
                    NationalId = dto.NationalId,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                    Role = UserRole.Doctor,
                    CreatedByUserId = createdByUserId,
                    MustChangePassword = true
                };

                _context.SystemUsers.Add(user);
                await _context.SaveChangesAsync();

                var doctor = new Doctor
                {
                    FullName = dto.FullName,
                    Email = "",
                    Phone = dto.Phone,
                    UniversityStaffId = universityId,
                    Code = universityId,          // ← تأكيد الـ Code بنفس universityId
                    DepartmentId = department.Id,   // Resolved from DepartmentCode
                    SystemUserId = user.Id
                };

                _context.Doctors.Add(doctor);
                await _context.SaveChangesAsync();

                await transaction.CommitAsync();

                var response = await GenerateAuthResponseAsync(user);
                response.UniversityEmail = universityEmail;
                response.GeneratedUniversityId = universityId;
                response.TemporaryPassword = password;

                return response;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<AuthResponseDto> RegisterAdminAsync(RegisterAdminDto dto, Ulid createdByUserId)
        {
            // Transactional for Admin as well
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                string password = GeneratePassword();
                string universityEmail = await GenerateUniversityEmailAsync("admin", UserRole.Admin);
                string universityId = await GenerateUniversityIdAsync(UserRole.Admin);

                if (await _context.SystemUsers.AnyAsync(u => u.NationalId == dto.NationalId))
                    throw new InvalidOperationException("NationalId already exists.");

                var user = new SystemUser
                {
                    FullName = dto.FullName,
                    UniversityEmail = universityEmail,
                    Email = universityEmail,
                    NationalId = dto.NationalId,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                    Role = UserRole.Admin,
                    CreatedByUserId = createdByUserId,
                    MustChangePassword = true
                };

                _context.SystemUsers.Add(user);
                await _context.SaveChangesAsync();

                var admin = new Admin
                {
                    FullName = dto.FullName,
                    Email = universityEmail, // Admin still has Email property? Yes, checking definition... Yes.
                                             // Maps to UniversityEmail
                    Phone = dto.Phone,
                    SystemUserId = user.Id
                };

                _context.Admins.Add(admin);
                await _context.SaveChangesAsync();

                await transaction.CommitAsync();

                var response = await GenerateAuthResponseAsync(user);
                response.UniversityEmail = universityEmail;
                response.GeneratedPassword = password; // Using GeneratedPassword for Admin
                response.GeneratedUniversityId = universityId;

                return response;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        private async Task<AuthResponseDto> GenerateAuthResponseAsync(SystemUser user)
        {
            var (token, jti) = await GenerateJwtToken(user);
            var refreshToken = new RefreshToken
            {
                JwtId = jti,
                Used = false,
                Invalidated = false,
                UserId = user.Id,
                CreationDate = DateTime.UtcNow,
                ExpiryDate = DateTime.UtcNow.AddMonths(6),
                Token = GenerateRandomString(35) + Guid.NewGuid()
            };

            await _context.RefreshTokens.AddAsync(refreshToken);
            await _context.SaveChangesAsync();

            return new AuthResponseDto
            {
                Token = token,
                RefreshToken = refreshToken.Token,
                Email = user.Email,
                Role = user.Role.ToString(),
                UserId = user.Id,
                FullName = user.FullName
            };
        }

        private async Task<(string Token, string Id)> GenerateJwtToken(SystemUser user)
        {
            var key = Encoding.UTF8.GetBytes(_jwtSecret);

            var jti = Guid.NewGuid().ToString();

            // Determine ProfileId and ProfileType
            Ulid? profileId = null;
            string profileType = user.Role.ToString();

            if (user.Role == UserRole.Student)
            {
                var profile = await _context.Students.AsNoTracking().FirstOrDefaultAsync(s => s.SystemUserId == user.Id);
                profileId = profile?.Id;
            }
            else if (user.Role == UserRole.Doctor)
            {
                var profile = await _context.Doctors.AsNoTracking().FirstOrDefaultAsync(d => d.SystemUserId == user.Id);
                profileId = profile?.Id;
            }
            else if (user.Role == UserRole.Admin)
            {
                var profile = await _context.Admins.AsNoTracking().FirstOrDefaultAsync(a => a.SystemUserId == user.Id);
                profileId = profile?.Id;
            }
            // SuperAdmin might not have a profile, or we treat SystemUser.Id as ProfileId? 
            // Better to keep 0 or SystemUserId if no profile exists.

            // Ensure Claims Correctness
            var claims = new List<Claim>
            {
                new(JwtRegisteredClaimNames.Sub, user.Email), // Subject is Email
                new(JwtRegisteredClaimNames.Jti, jti),
                new("nameid", user.Id.ToString()), // Requirements: NameIdentifier = user.Id
                new("role", user.Role.ToString()), // Requirements: Role = user.Role
                new("ProfileId", profileId?.ToString() ?? string.Empty),
                new("ProfileType", profileType)
            };

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddHours(_jwtExpirationHours),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature),
                Issuer = _jwtIssuer,
                Audience = _jwtAudience
            };

            var tokenHandler = new JwtSecurityTokenHandler();
            var token = tokenHandler.CreateToken(tokenDescriptor);
            return (tokenHandler.WriteToken(token), jti);
        }

        private ClaimsPrincipal? GetPrincipalFromToken(string token)
        {
            var tokenValidationParameters = new TokenValidationParameters
            {
                ValidateAudience = false, // Sometimes useful to relax for refresh, but let's keep it safe or strict if needed.
                ValidateIssuer = false,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSecret)),
                ValidateLifetime = false // Critical for refresh
            };

            var tokenHandler = new JwtSecurityTokenHandler();
            var principal = tokenHandler.ValidateToken(token, tokenValidationParameters, out var securityToken);
            if (securityToken is not JwtSecurityToken jwtSecurityToken || !jwtSecurityToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.InvariantCultureIgnoreCase))
                throw new SecurityTokenException("Invalid token");

            return principal;
        }

        private static string GenerateRandomString(int length)
        {
            var chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            return new string(Random.Shared.GetItems(chars.ToCharArray(), length));
        }

        public async Task<string> ResetPasswordAsync(Ulid targetUserId)
        {
            var user = await _context.SystemUsers.FindAsync(targetUserId)
                ?? throw new KeyNotFoundException($"User '{targetUserId}' not found.");

            var newPassword = GeneratePassword();
            user.PasswordHash       = BCrypt.Net.BCrypt.HashPassword(newPassword);
            user.MustChangePassword = true;   // force change on next login
            user.AccessFailedCount  = 0;
            user.LockoutEnd         = null;   // unlock if was locked

            await _context.SaveChangesAsync();
            return newPassword;
        }

        private static string GeneratePassword()
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!@#$%^&*";
            return new string(Random.Shared.GetItems(chars.ToCharArray(), 12));
        }

        private async Task<string> GenerateUniversityEmailAsync(string prefix, UserRole role)
        {
            // Standardized logic: prefixN@uni.edu
            var university = await _context.Universities.FirstOrDefaultAsync();
            var uniName = university?.Name.ToLower().Replace(" ", "") ?? "university";

            // Should prefix depend on name? User said: "Generate UniversityEmail based on university name + role prefix + incremental number"
            // I'll use role prefix.

            int count = await _context.SystemUsers.IgnoreQueryFilters().CountAsync(u => u.Role == role) + 1;
            string email;
            do
            {
                email = $"{prefix}{count}@{uniName}.edu";
                count++;
            } while (await _context.SystemUsers.IgnoreQueryFilters().AnyAsync(u => u.Email == email)); // Check against Email (which holds UniversityEmail)

            return email;
        }

        private async Task<string> GenerateUniversityIdAsync(UserRole role)
        {
            var year = DateTime.UtcNow.Year;
            int count = 0;
            string prefix = "";

            if (role == UserRole.Student)
            {
                prefix = "STU";
                count = await _context.Students.IgnoreQueryFilters().CountAsync() + 1;
            }
            else if (role == UserRole.Doctor)
            {
                prefix = "DOC";
                count = await _context.Doctors.IgnoreQueryFilters().CountAsync() + 1;
            }
            else if (role == UserRole.Admin)
            {
                prefix = "ADM";
                count = await _context.Admins.IgnoreQueryFilters().CountAsync() + 1;
            }

            return $"{prefix}{year}{count:D4}"; // STU20250001
        }
    }
}
