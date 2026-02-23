using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using UniversityManagementSystem.Core.Entities;
using UniversityManagementSystem.Core.Interfaces;
using UniversityManagementSystem.Infrastructure.Data;

namespace UniversityManagementSystem.Infrastructure.Services
{
    public class GradeService(AppDbContext context, IAuditService auditService) : IGradeService
    {
        private readonly IAuditService _auditService = auditService;
        public async Task<int> CalculateGradesForOfferingAsync(int offeringId, int doctorId)
        {
            // 1. Validate Doctor & Offering
            var offering = await context.Set<SubjectOffering>()
                .Include(so => so.Subject)
                .AsNoTracking()
                .FirstOrDefaultAsync(so => so.Id == offeringId)
                ?? throw new KeyNotFoundException($"SubjectOffering with ID {offeringId} not found.");

            if (offering.DoctorId != doctorId)
                throw new UnauthorizedAccessException("You are not the instructor for this offering.");

            // 2. Get Enrolled Students
            var enrollments = await context.Enrollments
                .Where(e => e.SubjectOfferingId == offeringId)
                .Select(e => e.StudentId)
                .ToListAsync();

            if (enrollments.Count == 0) return 0;

            int processedCount = 0;

            foreach (var studentId in enrollments)
            {
                await CalculateStudentGradeInternalAsync(studentId, offeringId);
                processedCount++;
            }

            await context.SaveChangesAsync();
            return processedCount;
        }

        private async Task CalculateStudentGradeInternalAsync(int studentId, int offeringId)
        {
            // 3. Get Student's Submissions for this offering
            var totalScore = await context.ExamSubmissions
                .Where(s => s.StudentId == studentId && s.Exam.SubjectOfferingId == offeringId)
                .SumAsync(s => s.Score ?? 0);

            // 4. Calculate Grade
            var (letter, points) = CalculateGradeScale(totalScore);

            // 5. Update or Create StudentGrade
            var gradeRecord = await context.Set<StudentGrade>()
                .IgnoreQueryFilters() // Include soft-deleted ones to restore them if needed? Or just create new?
                // Let's stick to active ones for now, OR if invalidating means soft-delete, then re-calc should restore?
                // If I soft-delete, query filter hides it. So I create new.
                // If I want to "Recalculate" a deleted one, I should probably restore it.
                // Let's use IgnoreQueryFilters to check.
                .FirstOrDefaultAsync(g => g.StudentId == studentId && g.SubjectOfferingId == offeringId);

            if (gradeRecord == null)
            {
                gradeRecord = new StudentGrade
                {
                    StudentId = studentId,
                    SubjectOfferingId = offeringId,
                    FinalScore = totalScore,
                    GradeLetter = letter,
                    GradePoints = points,
                    IsFinalized = true,
                    CalculatedAt = DateTime.UtcNow
                };
                context.Set<StudentGrade>().Add(gradeRecord);
            }
            else
            {
                // If it was soft-deleted, we might want to "restore" it implicitly?
                if (gradeRecord.DeletedAt != null)
                {
                    gradeRecord.DeletedAt = null;
                }

                gradeRecord.FinalScore = totalScore;
                gradeRecord.GradeLetter = letter;
                gradeRecord.GradePoints = points;
                gradeRecord.IsFinalized = true;
                gradeRecord.CalculatedAt = DateTime.UtcNow;
                
                context.Entry(gradeRecord).State = EntityState.Modified;
            }
        }

        public async Task RecalculateStudentGradeAsync(int gradeId)
        {
            var grade = await context.Set<StudentGrade>()
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(g => g.Id == gradeId)
                ?? throw new KeyNotFoundException($"StudentGrade {gradeId} not found.");

            await CalculateStudentGradeInternalAsync(grade.StudentId, grade.SubjectOfferingId);
            await context.SaveChangesAsync();
        }

        public async Task InvalidateGradeAsync(int gradeId)
        {
            var grade = await context.Set<StudentGrade>().FindAsync(gradeId)
                ?? throw new KeyNotFoundException($"StudentGrade {gradeId} not found.");
            
            var oldValues = System.Text.Json.JsonSerializer.Serialize(new { grade.FinalScore, grade.DeletedAt });

            grade.DeletedAt = DateTime.UtcNow;
            context.Entry(grade).State = EntityState.Modified;
            await context.SaveChangesAsync();

            await _auditService.LogAsync("SoftDelete", "StudentGrade", gradeId.ToString(), oldValues, null, null);
        }

        public async Task<UniversityManagementSystem.Core.DTOs.GradeDto> UpdateGradeAsync(int gradeId, UniversityManagementSystem.Core.DTOs.UpdateGradeDto dto)
        {
            var grade = await context.Set<StudentGrade>()
                .Include(g => g.Student)
                .Include(g => g.SubjectOffering)
                    .ThenInclude(so => so.Subject)
                .FirstOrDefaultAsync(g => g.Id == gradeId)
                ?? throw new KeyNotFoundException($"StudentGrade {gradeId} not found.");

            var oldValues = System.Text.Json.JsonSerializer.Serialize(new { grade.FinalScore, grade.GradeLetter, grade.GradePoints, grade.IsFinalized });

            grade.FinalScore = dto.FinalScore;
            grade.GradeLetter = dto.GradeLetter;
            grade.GradePoints = dto.GradePoints;
            grade.IsFinalized = dto.IsFinalized;
            grade.CalculatedAt = DateTime.UtcNow;

            context.Entry(grade).State = EntityState.Modified;
            await context.SaveChangesAsync();

            var newValues = System.Text.Json.JsonSerializer.Serialize(new { grade.FinalScore, grade.GradeLetter, grade.GradePoints, grade.IsFinalized });
            await _auditService.LogAsync("Update", "StudentGrade", gradeId.ToString(), oldValues, newValues, null);

            return new UniversityManagementSystem.Core.DTOs.GradeDto
            {
                Id = grade.Id,
                StudentId = grade.StudentId,
                StudentName = grade.Student?.FullName ?? "Unknown",
                SubjectOfferingId = grade.SubjectOfferingId,
                SubjectName = grade.SubjectOffering?.Subject?.Name ?? "Unknown",
                FinalScore = grade.FinalScore,
                GradeLetter = grade.GradeLetter,
                GradePoints = grade.GradePoints,
                IsFinalized = grade.IsFinalized,
                CalculatedAt = grade.CalculatedAt
            };
        }

        private static (string Letter, double Points) CalculateGradeScale(double score)
        {
            if (score >= 90) return ("A", 4.0);
            if (score >= 80) return ("B", 3.0);
            if (score >= 70) return ("C", 2.0);
            if (score >= 60) return ("D", 1.0);
            return ("F", 0.0);
        }

        public async Task<UniversityManagementSystem.Core.DTOs.StudentGpaDto> CalculateStudentGpaAsync(int studentId)
        {
            // 1. Get Student + Grades + Subject Info (CreditHours)
            var student = await context.Students
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == studentId)
                ?? throw new KeyNotFoundException($"Student with ID {studentId} not found.");

            var grades = await context.Set<StudentGrade>()
                .AsNoTracking()
                .Include(g => g.SubjectOffering)
                .ThenInclude(so => so.Subject)
                .Where(g => g.StudentId == studentId && g.IsFinalized)
                .ToListAsync();

            if (grades.Count == 0)
            {
                return new UniversityManagementSystem.Core.DTOs.StudentGpaDto
                {
                    StudentId = studentId,
                    StudentName = student.FullName,
                    GPA = 0.0,
                    TotalCreditHours = 0,
                    TotalSubjects = 0
                };
            }

            // 2. Calculate Weighted Sum
            double totalWeightedPoints = 0;
            int totalCreditHours = 0;

            foreach (var grade in grades)
            {
                // Ensure we have credit hours. If Subject is missing, skip? Should not happen due to FK constraints.
                // Assuming SubjectOffering -> Subject is mandatory.
                int credits = grade.SubjectOffering?.Subject?.CreditHours ?? 0;
                
                if (credits > 0)
                {
                    totalWeightedPoints += (grade.GradePoints * credits);
                    totalCreditHours += credits;
                }
            }

            // 3. Compute GPA
            double gpa = totalCreditHours > 0 ? totalWeightedPoints / totalCreditHours : 0.0;
            
            // Round to 2 decimal places
            gpa = Math.Round(gpa, 2);

            return new UniversityManagementSystem.Core.DTOs.StudentGpaDto
            {
                StudentId = studentId,
                StudentName = student.FullName,
                GPA = gpa,
                TotalCreditHours = totalCreditHours,
                TotalSubjects = grades.Count
            };
        }
    }
}
