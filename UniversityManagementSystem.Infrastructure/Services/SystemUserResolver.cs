using Microsoft.EntityFrameworkCore;
using System;
using System.Security.Claims;
using System.Threading.Tasks;
using UniversityManagementSystem.Core.Entities;
using UniversityManagementSystem.Core.Interfaces;
using UniversityManagementSystem.Infrastructure.Data;
using NUlid;

namespace UniversityManagementSystem.Infrastructure.Services
{
    public class SystemUserResolver(AppDbContext context) : ISystemUserResolver
    {
        private readonly AppDbContext _context = context;

        public async Task<Ulid> ResolveSystemUserIdAsync(ClaimsPrincipal user)
        {
            var profileIdClaim = user.FindFirst("ProfileId")?.Value;
            var profileTypeClaim = user.FindFirst("ProfileType")?.Value;
            var nameIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? user.FindFirst("nameid")?.Value;

            if (string.IsNullOrEmpty(profileIdClaim) || !Ulid.TryParse(profileIdClaim, out var profileId))
                throw new UnauthorizedAccessException("Invalid ProfileId in token.");

            if (!Enum.TryParse<UserRole>(profileTypeClaim, true, out var role))
                throw new UnauthorizedAccessException("Invalid ProfileType in token.");

            Student? student = null;
            Doctor? doctor = null;
            TeachingAssistant? teachingAssistant = null;
            Admin? admin = null;
            Ulid? existingSystemUserId = null;

            switch (role)
            {
                case UserRole.Student:
                    student = await _context.Students.FindAsync(profileId);
                    existingSystemUserId = student?.SystemUserId;
                    break;
                case UserRole.Doctor:
                    doctor = await _context.Doctors.FindAsync(profileId);
                    existingSystemUserId = doctor?.SystemUserId;
                    break;
                case UserRole.TeachingAssistant:
                    teachingAssistant = await _context.TeachingAssistants.FindAsync(profileId);
                    existingSystemUserId = teachingAssistant?.SystemUserId;
                    break;
                case UserRole.Admin:
                    admin = await _context.Admins.FindAsync(profileId);
                    existingSystemUserId = admin?.SystemUserId;
                    break;
            }

            if (existingSystemUserId.HasValue && existingSystemUserId.Value != Ulid.Empty)
            {
                var exists = await _context.SystemUsers.AnyAsync(u => u.Id == existingSystemUserId.Value);
                if (exists) return existingSystemUserId.Value;
            }

            var newUser = new SystemUser
            {
                FullName = user.FindFirst(ClaimTypes.Name)?.Value ?? user.FindFirst("name")?.Value ?? "System User",
                Email = user.FindFirst(ClaimTypes.Email)?.Value ?? user.FindFirst("email")?.Value ?? nameIdClaim ?? profileId.ToString(),
                UniversityEmail = user.FindFirst(ClaimTypes.Email)?.Value ?? user.FindFirst("email")?.Value ?? nameIdClaim ?? profileId.ToString(),
                NationalId = nameIdClaim ?? Guid.NewGuid().ToString(),
                PasswordHash = string.Empty,
                Role = role,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            _context.SystemUsers.Add(newUser);
            await _context.SaveChangesAsync();

            switch (role)
            {
                case UserRole.Student:
                    if (student != null)
                    {
                        student.SystemUserId = newUser.Id;
                        _context.Students.Update(student);
                    }
                    break;
                case UserRole.Doctor:
                    if (doctor != null)
                    {
                        doctor.SystemUserId = newUser.Id;
                        _context.Doctors.Update(doctor);
                    }
                    break;
                case UserRole.TeachingAssistant:
                    if (teachingAssistant != null)
                    {
                        teachingAssistant.SystemUserId = newUser.Id;
                        _context.TeachingAssistants.Update(teachingAssistant);
                    }
                    break;
                case UserRole.Admin:
                    if (admin != null)
                    {
                        admin.SystemUserId = newUser.Id;
                        _context.Admins.Update(admin);
                    }
                    break;
            }

            await _context.SaveChangesAsync();

            return newUser.Id;
        }
    }
}
