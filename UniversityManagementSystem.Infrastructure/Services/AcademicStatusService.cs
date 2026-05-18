using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using NUlid;
using UniversityManagementSystem.Core.Entities;
using UniversityManagementSystem.Core.Interfaces;
using UniversityManagementSystem.Infrastructure.Data;

namespace UniversityManagementSystem.Infrastructure.Services
{
    public class AcademicStatusService(AppDbContext context) : IAcademicStatusService
    {
        // ── Default policy used when no DB record exists ─────────────────────
        private static readonly AcademicPolicy _defaultPolicy = new()
        {
            DefaultMaxHours   = 18,
            HonorMaxHours     = 21,
            WarningMaxHours   = 12,
            ProbationMaxHours = 9,
            WarningGpaThreshold   = 2.0,
            ProbationGpaThreshold = 1.5,
            HonorGpaThreshold     = 3.5,
            GraduationMinGpa      = 2.0
        };

        public async Task<AcademicPolicy> GetPolicyAsync(Ulid? departmentId = null)
        {
            // Try department-specific first, then global (DepartmentId == null)
            if (departmentId.HasValue)
            {
                var specific = await context.AcademicPolicies
                    .AsNoTracking()
                    .FirstOrDefaultAsync(p => p.DepartmentId == departmentId && p.DeletedAt == null);
                if (specific != null) return specific;
            }

            var global = await context.AcademicPolicies
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.DepartmentId == null && p.DeletedAt == null);

            return global ?? _defaultPolicy;
        }

        public async Task<StudentAcademicStatus> GetOrCreateAsync(Ulid studentId)
        {
            var status = await context.StudentAcademicStatuses
                .FirstOrDefaultAsync(s => s.StudentId == studentId);

            if (status != null) return status;

            status = new StudentAcademicStatus
            {
                StudentId = studentId,
                Standing  = AcademicStanding.Good
            };
            context.StudentAcademicStatuses.Add(status);
            await context.SaveChangesAsync();
            return status;
        }

        public async Task RecalculateAsync(Ulid studentId)
        {
            // 1. Load all finalized grades for this student
            var grades = await context.StudentGrades
                .AsNoTracking()
                .Include(g => g.SubjectOffering)
                    .ThenInclude(so => so.Subject)
                .Include(g => g.SubjectOffering)
                    .ThenInclude(so => so.Semester)
                .Where(g => g.StudentId == studentId && g.IsFinalized && g.DeletedAt == null)
                .ToListAsync();

            // 2. CGPA — weighted average over ALL finalized grades
            double cgpa = 0;
            int totalHours = 0;
            foreach (var g in grades)
            {
                int hrs = g.SubjectOffering?.Subject?.CreditHours ?? 0;
                if (hrs > 0) { cgpa += g.GradePoints * hrs; totalHours += hrs; }
            }
            cgpa = totalHours > 0 ? Math.Round(cgpa / totalHours, 2) : 0.0;

            // 3. Last semester GPA — grades from the most recent semester
            double semesterGpa = 0;
            var lastSemesterId = grades
                .Select(g => g.SubjectOffering?.SemesterId)
                .Where(id => id.HasValue)
                .GroupBy(id => id)
                .OrderByDescending(grp => grp.Key)
                .Select(grp => grp.Key)
                .FirstOrDefault();

            if (lastSemesterId.HasValue)
            {
                double semPoints = 0; int semHours = 0;
                foreach (var g in grades.Where(g => g.SubjectOffering?.SemesterId == lastSemesterId))
                {
                    int hrs = g.SubjectOffering?.Subject?.CreditHours ?? 0;
                    if (hrs > 0) { semPoints += g.GradePoints * hrs; semHours += hrs; }
                }
                semesterGpa = semHours > 0 ? Math.Round(semPoints / semHours, 2) : 0.0;
            }

            // 4. Earned hours (passing grades only — GradePoints >= 1.0)
            int earnedHours = grades
                .Where(g => g.GradePoints >= 1.0)
                .Sum(g => g.SubjectOffering?.Subject?.CreditHours ?? 0);

            // 5. Load or create status record
            var status = await GetOrCreateAsync(studentId);

            // 6. Get policy to determine standing
            var student = await context.Students.AsNoTracking().FirstOrDefaultAsync(s => s.Id == studentId);
            var policy  = await GetPolicyAsync(student?.DepartmentId);

            // 7. Determine standing based on CGPA
            var newStanding = cgpa switch
            {
                var g when g < policy.ProbationGpaThreshold => AcademicStanding.Probation,
                var g when g < policy.WarningGpaThreshold   => AcademicStanding.Warning,
                _                                            => AcademicStanding.Good
            };

            // Track warnings: if standing worsened, increment
            if (newStanding > status.Standing)  // enum order: Good < Warning < Probation
                status.WarningCount++;

            // 8. Update status
            status.GPA             = cgpa;
            status.CGPA            = cgpa;
            status.LastSemesterGPA = semesterGpa;
            status.LastCalculatedAt = DateTime.UtcNow;
            status.EarnedCreditHours = earnedHours;
            status.Standing        = newStanding;

            await context.SaveChangesAsync();
        }
    }
}
