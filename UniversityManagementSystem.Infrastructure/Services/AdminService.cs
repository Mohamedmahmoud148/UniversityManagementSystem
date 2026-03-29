using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using NUlid;
using UniversityManagementSystem.Core.DTOs;
using UniversityManagementSystem.Core.Interfaces;
using UniversityManagementSystem.Infrastructure.Data;

namespace UniversityManagementSystem.Infrastructure.Services
{
    public class AdminService : IAdminService
    {
        private readonly AppDbContext _context;

        public AdminService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<AdminDto>> GetAllAdminsAsync()
        {
            return await _context.Admins
                .Include(a => a.SystemUser)
                .Select(a => new AdminDto(
                    a.Id,
                    a.FullName,
                    a.Email,
                    a.Phone,
                    a.SystemUser.Role.ToString(),
                    a.SystemUser.IsActive,
                    a.CreatedAt
                ))
                .ToListAsync();
        }

        public async Task<AdminDto?> GetAdminByIdAsync(Ulid id)
        {
            var admin = await _context.Admins
                .Include(a => a.SystemUser)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (admin == null) return null;

            return new AdminDto(
                admin.Id,
                admin.FullName,
                admin.Email,
                admin.Phone,
                admin.SystemUser.Role.ToString(),
                admin.SystemUser.IsActive,
                admin.CreatedAt
            );
        }

        public async Task UpdateAdminAsync(Ulid id, UpdateAdminDto dto)
        {
            var admin = await _context.Admins
                .Include(a => a.SystemUser)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (admin == null) throw new KeyNotFoundException("Admin not found.");

            admin.FullName = dto.FullName;
            if (dto.Phone != null) admin.Phone = dto.Phone;

            admin.SystemUser.FullName = dto.FullName;

            await _context.SaveChangesAsync();
        }

        public async Task DeleteAdminAsync(Ulid id)
        {
            var admin = await _context.Admins
                .Include(a => a.SystemUser)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (admin == null) throw new KeyNotFoundException("Admin not found.");

            _context.Admins.Remove(admin);
            _context.SystemUsers.Remove(admin.SystemUser);

            await _context.SaveChangesAsync();
        }

        public async Task ToggleAdminStatusAsync(Ulid id, bool isActive)
        {
            var admin = await _context.Admins
                .Include(a => a.SystemUser)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (admin == null) throw new KeyNotFoundException("Admin not found.");

            admin.SystemUser.IsActive = isActive;

            await _context.SaveChangesAsync();
        }
    }
}
