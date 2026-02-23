using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using UniversityManagementSystem.Core.DTOs;
using UniversityManagementSystem.Core.Entities;
using UniversityManagementSystem.Core.Interfaces;
using UniversityManagementSystem.Infrastructure.Data;

namespace UniversityManagementSystem.Infrastructure.Services
{
    public class ExamService(AppDbContext context, IAuditService auditService) : IExamService
    {
        private readonly IAuditService _auditService = auditService;
        public async Task<int> SubmitExamAsync(int examId, int studentId, ExamSubmissionDto submissionDto)
        {
            // 1. Validate Exam exists & is active
            var exam = await context.Exams
                .AsNoTracking()
                .Include(e => e.SubjectOffering)
                .FirstOrDefaultAsync(e => e.Id == examId)
                ?? throw new KeyNotFoundException($"Exam with ID {examId} not found.");

            if (!exam.IsPublished)
                throw new UnauthorizedAccessException("Exam is not published.");

            var currentTime = DateTime.UtcNow;
            if (currentTime < exam.StartTime || currentTime > exam.EndTime)
                throw new InvalidOperationException("Exam is not currently active.");

            // 2. Validate Student Enrollment (Active)
            var isEnrolled = await context.Enrollments
                .AsNoTracking()
                .AnyAsync(e => e.StudentId == studentId && e.SubjectOfferingId == exam.SubjectOfferingId && e.IsActive);

            if (!isEnrolled)
                throw new UnauthorizedAccessException("You are not actively enrolled in this course.");

            // 3. Check for previous submission (Optimistic check)
            var existingSubmission = await context.ExamSubmissions
                .AsNoTracking()
                .AnyAsync(s => s.ExamId == examId && s.StudentId == studentId);

            if (existingSubmission)
                throw new InvalidOperationException("You have already submitted this exam.");

            // 4. Create Submission
            var submission = new ExamSubmission
            {
                ExamId = examId,
                StudentId = studentId,
                SubmittedAt = DateTime.UtcNow,
                IsGraded = false,
                Score = null,
                AnswersJson = System.Text.Json.JsonSerializer.Serialize(submissionDto.Answers)
            };

            context.ExamSubmissions.Add(submission);

            try
            {
                await context.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
            {
                // Handle concurrent submission attempts
                if (ex.InnerException != null && ex.InnerException.Message.Contains("IX_ExamSubmissions_ExamId_StudentId")) // Check for specific index violation if possible, or generic
                {
                     throw new InvalidOperationException("You have already submitted this exam.");
                }
                throw; // Re-throw if it's another error
            }

            return submission.Id;
        }

        public async Task<ExamDto> CreateExamAsync(int subjectOfferingId, CreateExamDto dto, int doctorId)
        {
            // 1. Validate SubjectOffering exists and Doctor is assigned
            var offering = await context.Set<SubjectOffering>()
                .AsNoTracking()
                .Include(so => so.Subject)
                .FirstOrDefaultAsync(so => so.Id == subjectOfferingId)
                ?? throw new KeyNotFoundException($"SubjectOffering with ID {subjectOfferingId} not found.");

            if (offering.DoctorId != doctorId)
                throw new UnauthorizedAccessException("You are not the instructor for this subject offering.");

            // 2. Map DTO to Entity
            var exam = new Exam
            {
                Title = dto.Title,
                Type = dto.Type,
                TotalMarks = dto.Questions.Sum(q => q.Mark), // Auto-calc total marks from questions
                StartTime = dto.StartTime,
                EndTime = dto.EndTime,
                IsPublished = dto.IsPublished,
                SubjectOfferingId = subjectOfferingId,
                CreatedAt = DateTime.UtcNow,
                Questions = [.. dto.Questions.Select(q => new ExamQuestion
                {
                    QuestionText = q.QuestionText,
                    CorrectAnswer = q.CorrectAnswer,
                    Mark = q.Mark,
                    CreatedAt = DateTime.UtcNow
                })]
            };

            // 3. Save to DB
            context.Exams.Add(exam);
            await context.SaveChangesAsync();

            // 4. Map back to DTO
            return MapToDto(exam);
        }

        public async Task<ExamDto> GetExamByIdAsync(int examId, int userId, string userRole)
        {
            var exam = await context.Exams
                .AsNoTracking()
                .Include(e => e.Questions)
                .Include(e => e.SubjectOffering)
                    .ThenInclude(so => so.Subject)
                .FirstOrDefaultAsync(e => e.Id == examId)
                ?? throw new KeyNotFoundException($"Exam with ID {examId} not found.");

            // SAFEGUARD: Ensure SubjectOffering is not null before checking DoctorId
            if (exam.SubjectOffering is null)
                throw new KeyNotFoundException("Associated Subject Offering not found for this exam.");

            // 5. Security Checks
            if (userRole == "Doctor")
            {
                // Doctor must own the offering
                if (exam.SubjectOffering.DoctorId != userId)
                    throw new UnauthorizedAccessException("You are not authorized to view this exam.");
            }
            else if (userRole == "Student")
            {
                // Student must be enrolled
                var isEnrolled = await context.Enrollments
                    .AsNoTracking()
                    .AnyAsync(e => e.StudentId == userId && e.SubjectOfferingId == exam.SubjectOfferingId && e.IsActive);

                if (!isEnrolled)
                    throw new UnauthorizedAccessException("You are not actively enrolled in this course.");
                
                // Optional: Check if exam is published
                if (!exam.IsPublished)
                    throw new UnauthorizedAccessException("This exam is not published yet.");
            }
            // Admins can bypass

            return MapToDto(exam);
        }

        private static ExamDto MapToDto(Exam exam)
        {
            return new ExamDto
            {
                Id = exam.Id,
                Title = exam.Title,
                Type = exam.Type.ToString(),
                TotalMarks = exam.TotalMarks,
                StartTime = exam.StartTime,
                EndTime = exam.EndTime,
                IsPublished = exam.IsPublished,
                SubjectOfferingId = exam.SubjectOfferingId,
                SubjectName = exam.SubjectOffering?.Subject?.Name ?? string.Empty,
                // SAFEGUARD: Handle potentially null Questions collection
                Questions = exam.Questions?.Select(q => new ExamQuestionDto
                {
                    Id = q.Id,
                    QuestionText = q.QuestionText,
                    Mark = q.Mark
                }).ToList() ?? []
            };
        }
        public async Task<IEnumerable<ExamDto>> GetExamsByDoctorAsync(int doctorId)
        {
            var exams = await context.Exams
                .AsNoTracking()
                .Include(e => e.SubjectOffering)
                .ThenInclude(so => so.Subject)
                .Where(e => e.SubjectOffering.DoctorId == doctorId)
                .OrderByDescending(e => e.StartTime)
                .ToListAsync();

            return exams.Select(MapToDto);
        }

        public async Task<IEnumerable<ExamDto>> GetExamsByOfferingAsync(int offeringId, int doctorId)
        {
            // Validate Doctor owns the offering
            var offering = await context.Set<SubjectOffering>()
                .AsNoTracking()
                .FirstOrDefaultAsync(so => so.Id == offeringId)
                ?? throw new KeyNotFoundException($"SubjectOffering with ID {offeringId} not found.");

            if (offering.DoctorId != doctorId)
                throw new UnauthorizedAccessException("You are not the instructor for this offering.");

            var exams = await context.Exams
                .AsNoTracking()
                .Include(e => e.SubjectOffering)
                .ThenInclude(so => so.Subject)
                .Where(e => e.SubjectOfferingId == offeringId)
                .OrderByDescending(e => e.StartTime)
                .ToListAsync();

            return exams.Select(MapToDto);
        }

        public async Task<IEnumerable<ExamSubmissionResponseDto>> GetExamSubmissionsAsync(int examId, int doctorId)
        {
            var exam = await context.Exams
                .AsNoTracking()
                .Include(e => e.SubjectOffering)
                .FirstOrDefaultAsync(e => e.Id == examId)
                ?? throw new KeyNotFoundException($"Exam with ID {examId} not found.");

            if (exam.SubjectOffering.DoctorId != doctorId)
                throw new UnauthorizedAccessException("You are not authorized to view results for this exam.");

            var submissions = await context.ExamSubmissions
                .AsNoTracking()
                .Include(s => s.Student)
                .Where(s => s.ExamId == examId)
                .ToListAsync();

            return submissions.Select(s => new ExamSubmissionResponseDto
            {
                Id = s.Id,
                ExamId = s.ExamId,
                StudentId = s.StudentId,
                StudentName = s.Student.FullName,
                SubmittedAt = s.SubmittedAt,
                Score = s.Score,
                IsGraded = s.IsGraded,
                AnswersJson = s.AnswersJson
            });
        }

        public async Task<IEnumerable<ExamDto>> GetStudentEnrolledExamsAsync(int studentId)
        {
            // 1. Get Offerings student is enrolled in
            var enrolledOfferingIds = await context.Enrollments
                .AsNoTracking()
                .Where(e => e.StudentId == studentId && e.IsActive)
                .Select(e => e.SubjectOfferingId)
                .ToListAsync();

            // 2. Get Exams for these offerings
            var exams = await context.Exams
                .AsNoTracking()
                .Include(e => e.SubjectOffering)
                .ThenInclude(so => so.Subject)
                .Where(e => enrolledOfferingIds.Contains(e.SubjectOfferingId) && e.IsPublished)
                .OrderBy(e => e.StartTime)
                .ToListAsync();

            return exams.Select(MapToDto);
        }

        public async Task<ExamSubmissionResponseDto?> GetStudentSubmissionAsync(int examId, int studentId)
        {
            var submission = await context.ExamSubmissions
                .AsNoTracking()
                .Include(s => s.Student) // Include for consistency, though we know the student
                .FirstOrDefaultAsync(s => s.ExamId == examId && s.StudentId == studentId);

            if (submission == null) return null;

            return new ExamSubmissionResponseDto
            {
                Id = submission.Id,
                ExamId = submission.ExamId,
                StudentId = submission.StudentId,
                StudentName = submission.Student.FullName,
                SubmittedAt = submission.SubmittedAt,
                Score = submission.Score,
                IsGraded = submission.IsGraded,
                AnswersJson = submission.AnswersJson
            };
        }

        public async Task GradeSubmissionAsync(GradeSubmissionDto dto, int doctorId)
        {
            var submission = await context.ExamSubmissions
                .Include(s => s.Exam)
                .ThenInclude(e => e.SubjectOffering)
                .FirstOrDefaultAsync(s => s.Id == dto.SubmissionId)
                ?? throw new KeyNotFoundException($"Submission with ID {dto.SubmissionId} not found.");

            // Validate Doctor owns the exam
            if (submission.Exam.SubjectOffering.DoctorId != doctorId)
                throw new UnauthorizedAccessException("You are not authorized to grade this submission.");

            // Update Score
            submission.Score = dto.Score;
            submission.IsGraded = true;

            await context.SaveChangesAsync();
        }

        public async Task<int> AutoGradeExamAsync(int examId, int doctorId)
        {
            // 1. Get Exam with Questions and Submissions
            var exam = await context.Exams
                .Include(e => e.Questions)
                .Include(e => e.SubjectOffering)
                .Include(e => e.Submissions) // We need tracking here to update them
                .FirstOrDefaultAsync(e => e.Id == examId)
                ?? throw new KeyNotFoundException($"Exam with ID {examId} not found.");

            if (exam.SubjectOffering.DoctorId != doctorId)
                throw new UnauthorizedAccessException("You are not authorized to auto-grade this exam.");

            int gradedCount = 0;

            // 2. Loop through submissions
            foreach (var submission in exam.Submissions)
            {
                if (submission.IsGraded) continue; // Skip already graded? Or re-grade? Let's re-grade if requested.
                // Actually, let's re-grade to allow corrections.

                var answers = System.Text.Json.JsonSerializer.Deserialize<List<ExamAnswerDto>>(submission.AnswersJson);
                
                if (answers == null || answers.Count == 0)
                {
                    submission.Score = 0;
                    submission.IsGraded = true;
                    gradedCount++;
                    continue;
                }

                double totalScore = 0;

                foreach (var question in exam.Questions)
                {
                    var studentAnswer = answers.FirstOrDefault(a => a.QuestionId == question.Id);
                    if (studentAnswer != null)
                    {
                        // Simple exact match (Case-Insensitive)
                        if (string.Equals(studentAnswer.AnswerText.Trim(), question.CorrectAnswer.Trim(), StringComparison.OrdinalIgnoreCase))
                        {
                            totalScore += question.Mark;
                        }
                    }
                }

                submission.Score = totalScore;
                submission.IsGraded = true;
                gradedCount++;
            }

            await context.SaveChangesAsync();
            return gradedCount;
        }


        public async Task ArchiveExamAsync(int examId)
        {
            var exam = await context.Exams.FindAsync(examId) 
                       ?? throw new KeyNotFoundException($"Exam {examId} not found.");
            
            exam.DeletedAt = DateTime.UtcNow;
            context.Entry(exam).State = EntityState.Modified;
            await context.SaveChangesAsync();
        }

        public async Task RestoreExamAsync(int examId)
        {
            var exam = await context.Exams.IgnoreQueryFilters().FirstOrDefaultAsync(e => e.Id == examId)
                       ?? throw new KeyNotFoundException($"Exam {examId} not found.");
            
            exam.DeletedAt = null;
            context.Entry(exam).State = EntityState.Modified;
            await context.SaveChangesAsync();
            
            await _auditService.LogAsync("Restore", "Exam", examId.ToString(), null, "Restored", null);
        }

        public async Task<ExamDto> UpdateExamAsync(int examId, UpdateExamDto dto)
        {
            var exam = await context.Exams
                .Include(e => e.SubjectOffering)
                .ThenInclude(so => so.Subject)
                .FirstOrDefaultAsync(e => e.Id == examId)
                ?? throw new KeyNotFoundException($"Exam {examId} not found.");

            var oldValues = System.Text.Json.JsonSerializer.Serialize(new { exam.Title, exam.Type, exam.StartTime, exam.EndTime, exam.IsPublished });

            exam.Update(dto.Title, dto.Type, dto.StartTime, dto.EndTime, dto.IsPublished);
            
            context.Entry(exam).State = EntityState.Modified;
            await context.SaveChangesAsync();

            var newValues = System.Text.Json.JsonSerializer.Serialize(new { exam.Title, exam.Type, exam.StartTime, exam.EndTime, exam.IsPublished });
            await _auditService.LogAsync("Update", "Exam", examId.ToString(), oldValues, newValues, null);

            return MapToDto(exam);
        }

        public async Task DeleteExamAsync(int examId)
        {
            var exam = await context.Exams
                .Include(e => e.Submissions)
                .FirstOrDefaultAsync(e => e.Id == examId)
                ?? throw new KeyNotFoundException($"Exam {examId} not found.");

            // Safety check: Cannot delete if submissions exist
            if (exam.Submissions.Any())
                throw new InvalidOperationException("Cannot delete exam because it already has student submissions.");

            var oldValues = System.Text.Json.JsonSerializer.Serialize(new { exam.Title, exam.DeletedAt });

            exam.DeletedAt = DateTime.UtcNow;
            context.Entry(exam).State = EntityState.Modified;
            await context.SaveChangesAsync();

            await _auditService.LogAsync("SoftDelete", "Exam", examId.ToString(), oldValues, null, null);
        }
    }
}
