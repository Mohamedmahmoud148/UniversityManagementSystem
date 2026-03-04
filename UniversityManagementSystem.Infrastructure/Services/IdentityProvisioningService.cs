using Microsoft.EntityFrameworkCore;
using Polly;
using System;
using System.Linq;
using System.Threading.Tasks;
using UniversityManagementSystem.Core.Entities;
using UniversityManagementSystem.Core.Interfaces;
using UniversityManagementSystem.Infrastructure.Data;

namespace UniversityManagementSystem.Infrastructure.Services
{
    public class IdentityProvisioningService(AppDbContext context, ISmartStringGenerator smartString) : IIdentityProvisioningService
    {
        private readonly AppDbContext _context = context;
        private readonly ISmartStringGenerator _smartString = smartString;

        public async Task<string> GenerateStudentIdAsync(int batchId, int departmentId)
        {
            // Format: [Year][DeptCode][Batch][Sequence]
            // Example: 2026 CS 01 0001 -> 2026110001 (using numeric IDs for simplicity or map codes)
            // Simpler: [Year][Random/Seq]

            var year = DateTime.UtcNow.Year;

            // Retry policy for collision
            var policy = Policy
                .Handle<Exception>()
                .RetryAsync(3);

            return await policy.ExecuteAsync(async () =>
            {
                var count = await _context.Students.CountAsync(s => s.CreatedAt.Year == year) + 1;
                // Base ID without random noise, SmartStringGenerator will handle duplicates
                var baseId = $"{year}{departmentId:D2}{count:D4}";

                return await _smartString.GenerateUniqueAsync<Student>(baseId, s => s.UniversityStudentId);
            });
        }

        public async Task<string> GenerateUniversityEmailAsync(string firstName, string lastName, UserRole role)
        {
            var baseEmail = $"{firstName.ToLower().Trim()}.{lastName.ToLower().Trim()}";
            var domain = role == UserRole.Student ? "student.university.edu" : "university.edu";
            var email = $"{baseEmail}@{domain}";

            int counter = 1;
            while (await _context.SystemUsers.AnyAsync(u => u.Email == email))
            {
                email = $"{baseEmail}{counter}@{domain}";
                counter++;
            }

            return email;
        }

        public async Task<string> GenerateStaffIdAsync(int departmentId)
        {
            var year = DateTime.UtcNow.Year;
            var policy = Policy.Handle<Exception>().RetryAsync(3);

            return await policy.ExecuteAsync(async () =>
            {
                var count = await _context.Doctors.CountAsync(d => d.CreatedAt.Year == year) + 1;
                var baseId = $"STAFF{year}{departmentId:D2}{count:D3}";

                return await _smartString.GenerateUniqueAsync<Doctor>(baseId, d => d.UniversityStaffId);
            });
        }

        public string GenerateSecurePassword(int length = 12)
        {
            const string validChars = "ABCDEFGHJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!@#$%^&*?";
            var random = new Random();
            var chars = new char[length];
            for (int i = 0; i < length; i++)
            {
                chars[i] = validChars[random.Next(validChars.Length)];
            }
            return new string(chars);
        }
    }
}
