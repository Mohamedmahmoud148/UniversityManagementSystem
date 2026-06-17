using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using UniversityManagementSystem.Core.Entities;
using UniversityManagementSystem.Core.Interfaces;
using UniversityManagementSystem.Infrastructure.Data;
using NUlid;

namespace UniversityManagementSystem.Infrastructure.Services
{
    public class GradeService(AppDbContext context, IAuditService auditService,
        IAcademicStatusService academicStatusService) : IGradeService
    {
        private readonly IAuditService _auditService = auditService;
        public async Task<int> CalculateGradesForOfferingAsync(Ulid offeringId, Ulid doctorId)
        {
            // 1. Validate Doctor & Offering with grading config
            var offering = await context.Set<SubjectOffering>()
                .Include(so => so.Subject)
                .AsNoTracking()
                .FirstOrDefaultAsync(so => so.Id == offeringId)
                ?? throw new KeyNotFoundException($"SubjectOffering with ID {offeringId} not found.");

            if (offering.DoctorId != doctorId)
                throw new UnauthorizedAccessException("You are not the instructor for this offering.");

            // 1b. Validate Weights = 1.0 (with a small epsilon for floating point)
            double totalWeight = offering.MidtermWeight + offering.CourseworkWeight + offering.FinalExamWeight + offering.PlatformWeight;
            if (Math.Abs(totalWeight - 1.0) > 0.001)
            {
                throw new InvalidOperationException($"Total weights must equal 1.0. Current sum is {totalWeight}. Please configure the grading weights first.");
            }

            // 2. Get Enrolled Students
            var enrolledStudentIds = await context.Enrollments
                .Where(e => e.SubjectOfferingId == offeringId)
                .Select(e => e.StudentId)
                .ToListAsync();

            if (enrolledStudentIds.Count == 0) return 0;

            // 3. Aggregate Platform Scores in one query (GroupBy)
            var platformScores = await context.ExamSubmissions
                .Where(s => s.Exam.SubjectOfferingId == offeringId && enrolledStudentIds.Contains(s.StudentId))
                .GroupBy(s => s.StudentId)
                .Select(g => new { StudentId = g.Key, TotalPlatformScore = g.Sum(s => s.Score ?? 0) })
                .ToDictionaryAsync(x => x.StudentId, x => x.TotalPlatformScore);

            // 4. Fetch existing Grade Records
            var existingGrades = await context.Set<StudentGrade>()
                .IgnoreQueryFilters()
                .Where(g => g.SubjectOfferingId == offeringId && enrolledStudentIds.Contains(g.StudentId))
                .ToDictionaryAsync(g => g.StudentId);

            int processedCount = 0;

            // 5. Process each student
            foreach (var studentId in enrolledStudentIds)
            {
                double platformScore = platformScores.TryGetValue(studentId, out var pScore) ? pScore : 0;
                
                if (!existingGrades.TryGetValue(studentId, out var gradeRecord))
                {
                    gradeRecord = new StudentGrade
                    {
                        StudentId = studentId,
                        SubjectOfferingId = offeringId,
                        PlatformScore = platformScore
                    };
                    context.Set<StudentGrade>().Add(gradeRecord);
                }
                else
                {
                    gradeRecord.PlatformScore = platformScore;
                    if (gradeRecord.DeletedAt != null) gradeRecord.DeletedAt = null;
                    context.Entry(gradeRecord).State = EntityState.Modified;
                }

                // Calculate weighted Final Score (normalized: score/max × 100 × weight)
                // This gives FinalScore on a 0-100 scale regardless of per-component max.
                double finalScore =
                    (offering.MidtermMaxScore   > 0 ? (gradeRecord.MidtermScore   ?? 0) / offering.MidtermMaxScore   * 100 * offering.MidtermWeight   : 0) +
                    (offering.CourseworkMaxScore > 0 ? (gradeRecord.CourseworkScore ?? 0) / offering.CourseworkMaxScore * 100 * offering.CourseworkWeight : 0) +
                    (offering.FinalExamMaxScore  > 0 ? (gradeRecord.FinalExamScore  ?? 0) / offering.FinalExamMaxScore  * 100 * offering.FinalExamWeight  : 0) +
                    (offering.PlatformMaxScore   > 0 ? (gradeRecord.PlatformScore   ?? 0) / offering.PlatformMaxScore   * 100 * offering.PlatformWeight   : 0);

                var (letter, points) = CalculateGradeScale(finalScore);

                gradeRecord.FinalScore = finalScore;
                gradeRecord.GradeLetter = letter;
                gradeRecord.GradePoints = points;
                gradeRecord.IsFinalized = true;
                gradeRecord.CalculatedAt = DateTime.UtcNow;

                processedCount++;
            }

            await context.SaveChangesAsync();

            // Auto-update persisted GPA for every affected student after finalization
            foreach (var studentId in enrolledStudentIds)
                await academicStatusService.RecalculateAsync(studentId);

            return processedCount;
        }

        private async Task CalculateStudentGradeInternalAsync(Ulid studentId, Ulid offeringId)
        {
            // Re-use bulk method for single calculation to keep logic DRY and consistent with weights.
            // But since this is called for single recalculations, we can fetch offering first.
            var offering = await context.Set<SubjectOffering>().FirstOrDefaultAsync(o => o.Id == offeringId)
                           ?? throw new KeyNotFoundException($"Offering {offeringId} not found");

            // We can just call the bulk method. It's safe since it aggregates for all enrolled.
            // However, to be highly optimized for a single student, we do it directly:
            double totalWeight = offering.MidtermWeight + offering.CourseworkWeight + offering.FinalExamWeight + offering.PlatformWeight;
            if (Math.Abs(totalWeight - 1.0) > 0.001) throw new InvalidOperationException("Weights must equal 1.0.");

            var platformScore = await context.ExamSubmissions
                .Where(s => s.StudentId == studentId && s.Exam.SubjectOfferingId == offeringId)
                .SumAsync(s => s.Score ?? 0);

            var gradeRecord = await context.Set<StudentGrade>()
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(g => g.StudentId == studentId && g.SubjectOfferingId == offeringId);

            if (gradeRecord == null)
            {
                gradeRecord = new StudentGrade { StudentId = studentId, SubjectOfferingId = offeringId };
                context.Set<StudentGrade>().Add(gradeRecord);
            }
            else if (gradeRecord.DeletedAt != null)
            {
                gradeRecord.DeletedAt = null;
                context.Entry(gradeRecord).State = EntityState.Modified;
            }

            gradeRecord.PlatformScore = platformScore;

            double finalScore =
                    (offering.MidtermMaxScore   > 0 ? (gradeRecord.MidtermScore   ?? 0) / offering.MidtermMaxScore   * 100 * offering.MidtermWeight   : 0) +
                    (offering.CourseworkMaxScore > 0 ? (gradeRecord.CourseworkScore ?? 0) / offering.CourseworkMaxScore * 100 * offering.CourseworkWeight : 0) +
                    (offering.FinalExamMaxScore  > 0 ? (gradeRecord.FinalExamScore  ?? 0) / offering.FinalExamMaxScore  * 100 * offering.FinalExamWeight  : 0) +
                    (offering.PlatformMaxScore   > 0 ? (gradeRecord.PlatformScore   ?? 0) / offering.PlatformMaxScore   * 100 * offering.PlatformWeight   : 0);

            var (letter, points) = CalculateGradeScale(finalScore);

            gradeRecord.FinalScore = finalScore;
            gradeRecord.GradeLetter = letter;
            gradeRecord.GradePoints = points;
            gradeRecord.IsFinalized = true;
            gradeRecord.CalculatedAt = DateTime.UtcNow;
        }

        public async Task RecalculateStudentGradeAsync(Ulid gradeId)
        {
            var grade = await context.Set<StudentGrade>()
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(g => g.Id == gradeId)
                ?? throw new KeyNotFoundException($"StudentGrade {gradeId} not found.");

            await CalculateStudentGradeInternalAsync(grade.StudentId, grade.SubjectOfferingId);
            await context.SaveChangesAsync();
        }

        public async Task InvalidateGradeAsync(Ulid gradeId)
        {
            var grade = await context.Set<StudentGrade>().FindAsync(gradeId)
                ?? throw new KeyNotFoundException($"StudentGrade {gradeId} not found.");

            var oldValues = System.Text.Json.JsonSerializer.Serialize(new { grade.FinalScore, grade.DeletedAt });

            grade.DeletedAt = DateTime.UtcNow;
            context.Entry(grade).State = EntityState.Modified;
            await context.SaveChangesAsync();

            await _auditService.LogAsync("SoftDelete", "StudentGrade", gradeId.ToString(), oldValues, null, null);
        }

        public async Task<UniversityManagementSystem.Core.DTOs.GradeDto> UpdateGradeAsync(Ulid gradeId, UniversityManagementSystem.Core.DTOs.UpdateGradeDto dto)
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

        public async Task<UniversityManagementSystem.Core.DTOs.StudentGpaDto> CalculateStudentGpaAsync(Ulid studentId)
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

        public async Task<System.Collections.Generic.IEnumerable<UniversityManagementSystem.Core.DTOs.GradeDto>> GetStudentGradesAsync(Ulid studentId)
        {
            var grades = await context.StudentGrades
                .AsNoTracking()
                .Include(g => g.SubjectOffering).ThenInclude(so => so.Subject)
                .Include(g => g.Student)
                .Where(g => g.StudentId == studentId)
                .OrderByDescending(g => g.CalculatedAt)
                .ToListAsync();

            return grades.Select(g => new UniversityManagementSystem.Core.DTOs.GradeDto
            {
                Id = g.Id,
                StudentId = g.StudentId,
                StudentName = g.Student?.FullName ?? string.Empty,
                SubjectOfferingId = g.SubjectOfferingId,
                SubjectName = g.SubjectOffering?.Subject?.Name ?? string.Empty,
                FinalScore = g.FinalScore,
                GradeLetter = g.GradeLetter,
                GradePoints = g.GradePoints,
                IsFinalized = g.IsFinalized,
                CalculatedAt = g.CalculatedAt
            });
        }
    }
}
