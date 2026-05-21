using System;
using NUlid;
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
    public class ExamService(AppDbContext context, IAuditService auditService, IAiService aiService, IFileService fileService) : IExamService
    {
        private readonly IAuditService _auditService = auditService;
        private readonly IAiService _aiService = aiService;
        private readonly IFileService _fileService = fileService;

        private async Task<string> GenerateUniqueExamCodeAsync()
        {
            var year = DateTime.UtcNow.Year;
            var prefix = $"EXAM-{year}-";

            var lastCode = await context.Exams
                .Where(e => e.Code.StartsWith(prefix))
                .OrderByDescending(e => e.Code)
                .Select(e => e.Code)
                .FirstOrDefaultAsync();

            int next = 1;
            if (lastCode != null)
            {
                var suffix = lastCode.Replace(prefix, "");
                if (int.TryParse(suffix, out int parsed))
                    next = parsed + 1;
            }

            return $"{prefix}{next:D4}";
        }
        public async Task<Ulid> SubmitExamAsync(Ulid examId, Ulid studentId, ExamSubmissionDto submissionDto)
        {
            // 1. Validate Exam exists & is active
            var exam = await context.Exams
                .AsNoTracking()
                .Include(e => e.SubjectOffering)
                .FirstOrDefaultAsync(e => e.Id == examId)
                ?? throw new KeyNotFoundException($"Exam with ID {examId} not found.");

            if (exam.Status != ExamStatus.Published)
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

        public async Task<ExamDto> CreateExamAsync(Ulid subjectOfferingId, CreateExamDto dto, Ulid doctorId)
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
                Code = await GenerateUniqueExamCodeAsync(),
                Title = dto.Title,
                Type = dto.Type,
                TotalMarks = dto.Questions.Sum(q => q.Mark),
                StartTime = dto.StartTime,
                EndTime = dto.EndTime,
                Status = dto.Status,
                Mode = ExamMode.Structured,
                CreatedByDoctorId = doctorId,
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

        public async Task<ExamDto?> GetExamByCodeAsync(string code, Ulid userId, string userRole)
        {
            var exam = await context.Exams
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.Code == code);

            if (exam == null) return null;

            return await GetExamByIdAsync(exam.Id, userId, userRole);
        }

        public async Task<ExamDto> GetExamByIdAsync(Ulid examId, Ulid userId, string userRole)
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
                if (exam.Status != ExamStatus.Published)
                    throw new UnauthorizedAccessException("This exam is not published yet.");
            }
            // Admins can bypass

            return MapToDto(exam);
        }

        private static ExamDto MapToDto(Exam exam, bool includeCorrectAnswers = false,
            IEnumerable<ExamQuestion>? filteredQuestions = null)
        {
            var questions = (filteredQuestions ?? exam.Questions ?? [])
                .Select(q => new ExamQuestionDto
                {
                    Id = q.Id,
                    QuestionText = q.QuestionText,
                    QuestionType = q.QuestionType.ToString(),
                    Options = q.OptionsJson != null
                        ? System.Text.Json.JsonSerializer.Deserialize<List<string>>(q.OptionsJson)
                        : null,
                    Mark = q.Mark,
                    CorrectAnswer = includeCorrectAnswers ? q.CorrectAnswer : null
                }).ToList();

            return new ExamDto
            {
                Id = exam.Id,
                Code = exam.Code,
                Title = exam.Title,
                Type = exam.Type.ToString(),
                TotalMarks = exam.TotalMarks,
                StartTime = exam.StartTime,
                EndTime = exam.EndTime,
                Mode = exam.Mode.ToString(),
                Status = exam.Status.ToString(),
                FilePath = exam.FilePath,
                CreatedByDoctorId = exam.CreatedByDoctorId,
                SubjectOfferingId = exam.SubjectOfferingId,
                SubjectName = exam.SubjectOffering?.Subject?.Name ?? string.Empty,
                IsRandomized = exam.IsRandomized,
                QuestionsPerStudent = exam.QuestionsPerStudent,
                Questions = questions
            };
        }
        public async Task<IEnumerable<ExamDto>> GetExamsByDoctorAsync(Ulid doctorId)
        {
            var exams = await context.Exams
                .AsNoTracking()
                .Include(e => e.SubjectOffering)
                .ThenInclude(so => so.Subject)
                .Where(e => e.SubjectOffering.DoctorId == doctorId)
                .OrderByDescending(e => e.StartTime)
                .ToListAsync();

            return exams.Select(e => MapToDto(e, includeCorrectAnswers: true));
        }

        public async Task<IEnumerable<ExamDto>> GetExamsByOfferingAsync(Ulid offeringId, Ulid doctorId)
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

            return exams.Select(e => MapToDto(e, includeCorrectAnswers: true));
        }

        public async Task<IEnumerable<ExamSubmissionResponseDto>> GetExamSubmissionsAsync(Ulid examId, Ulid doctorId)
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

        public async Task<IEnumerable<ExamDto>> GetStudentEnrolledExamsAsync(Ulid studentId)
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
                .Where(e => enrolledOfferingIds.Contains(e.SubjectOfferingId) && e.Status == ExamStatus.Published)
                .OrderBy(e => e.StartTime)
                .ToListAsync();

            return exams.Select(e => MapToDto(e));
        }

        public async Task<ExamDto> GetStudentVariantAsync(Ulid examId, Ulid studentId)
        {
            var exam = await context.Exams
                .AsNoTracking()
                .Include(e => e.Questions)
                .Include(e => e.SubjectOffering).ThenInclude(so => so.Subject)
                .FirstOrDefaultAsync(e => e.Id == examId)
                ?? throw new KeyNotFoundException($"Exam {examId} not found.");

            if (!exam.IsRandomized)
                return MapToDto(exam);

            var variant = await context.Set<StudentExamVariant>()
                .FirstOrDefaultAsync(v => v.ExamId == examId && v.StudentId == studentId);

            if (variant == null)
            {
                var allIds = exam.Questions.Select(q => q.Id).OrderBy(_ => Guid.NewGuid()).ToList();
                var count = exam.QuestionsPerStudent > 0 && exam.QuestionsPerStudent < allIds.Count
                    ? exam.QuestionsPerStudent : allIds.Count;
                var selectedIds = allIds.Take(count).ToList();

                variant = new StudentExamVariant
                {
                    ExamId = examId,
                    StudentId = studentId,
                    QuestionIdsJson = System.Text.Json.JsonSerializer.Serialize(selectedIds.Select(id => id.ToString()))
                };
                context.Set<StudentExamVariant>().Add(variant);
                await context.SaveChangesAsync();
            }

            var variantIds = System.Text.Json.JsonSerializer
                .Deserialize<List<string>>(variant.QuestionIdsJson)!
                .Select(Ulid.Parse).ToHashSet();
            var filtered = exam.Questions.Where(q => variantIds.Contains(q.Id));
            return MapToDto(exam, includeCorrectAnswers: false, filteredQuestions: filtered);
        }

        public async Task<ExamSubmissionResponseDto?> GetStudentSubmissionAsync(Ulid examId, Ulid studentId)
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

        public async Task GradeSubmissionAsync(GradeSubmissionDto dto, Ulid doctorId)
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

        public async Task<int> AutoGradeExamAsync(Ulid examId, Ulid doctorId)
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


        public async Task ArchiveExamAsync(Ulid examId)
        {
            var exam = await context.Exams.FindAsync(examId)
                       ?? throw new KeyNotFoundException($"Exam {examId} not found.");

            exam.DeletedAt = DateTime.UtcNow;
            context.Entry(exam).State = EntityState.Modified;
            await context.SaveChangesAsync();
        }

        public async Task RestoreExamAsync(Ulid examId)
        {
            var exam = await context.Exams.IgnoreQueryFilters().FirstOrDefaultAsync(e => e.Id == examId)
                       ?? throw new KeyNotFoundException($"Exam {examId} not found.");

            exam.DeletedAt = null;
            context.Entry(exam).State = EntityState.Modified;
            await context.SaveChangesAsync();

            await _auditService.LogAsync("Restore", "Exam", examId.ToString(), null, "Restored", null);
        }

        public async Task<ExamDto> UpdateExamAsync(Ulid examId, UpdateExamDto dto)
        {
            var exam = await context.Exams
                .Include(e => e.SubjectOffering)
                .ThenInclude(so => so.Subject)
                .FirstOrDefaultAsync(e => e.Id == examId)
                ?? throw new KeyNotFoundException($"Exam {examId} not found.");

            var oldValues = System.Text.Json.JsonSerializer.Serialize(new { exam.Title, exam.Type, exam.StartTime, exam.EndTime, exam.Status });

            exam.Update(dto.Title, dto.Type, dto.StartTime, dto.EndTime, dto.Status, exam.Mode, exam.FilePath);

            context.Entry(exam).State = EntityState.Modified;
            await context.SaveChangesAsync();

            var newValues = System.Text.Json.JsonSerializer.Serialize(new { exam.Title, exam.Type, exam.StartTime, exam.EndTime, exam.Status });
            await _auditService.LogAsync("Update", "Exam", examId.ToString(), oldValues, newValues, null);

            return MapToDto(exam);
        }

        public async Task DeleteExamAsync(Ulid examId)
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

        public async Task<ExamDto> GenerateAiExamAsync(CreateAiExamRequest request, Ulid doctorId)
        {
            var offering = await context.Set<SubjectOffering>()
                .AsNoTracking()
                .Include(so => so.Subject)
                .ThenInclude(s => s.Department)
                .Include(so => so.Subject.Batch)
                .FirstOrDefaultAsync(so => so.Id == request.SubjectOfferingId)
                ?? throw new KeyNotFoundException($"SubjectOffering {request.SubjectOfferingId} not found.");

            if (offering.DoctorId != doctorId)
                throw new UnauthorizedAccessException("You are not the instructor for this offering.");

            var structuredRequest = new UniversityManagementSystem.Core.DTOs.Ai.AiGenerateExamRequestDto
            {
                Subject = offering.Subject.Name,
                Department = offering.Subject.Department?.Name ?? "General",
                Batch = offering.Subject.Batch?.Name ?? "General",
                Difficulty = request.Difficulty,
                QuestionCount = request.QuestionCount,
                ExamType = request.ExamType,
                Topics = request.Topics
            };

            var questionsDto = await _aiService.GenerateExamAsync(structuredRequest);

            var exam = new Exam
            {
                Code = await GenerateUniqueExamCodeAsync(),
                Title = $"{offering.Subject.Name} - AI Generated Draft",
                Type = ExamType.Final,
                TotalMarks = questionsDto.Sum(q => q.Mark),
                StartTime = DateTime.UtcNow.AddDays(1),
                EndTime = DateTime.UtcNow.AddDays(1).AddHours(2),
                Status = ExamStatus.Draft,
                Mode = ExamMode.AI,
                CreatedByDoctorId = doctorId,
                SubjectOfferingId = request.SubjectOfferingId,
                IsRandomized = request.IsRandomized,
                QuestionsPerStudent = request.QuestionsPerStudent,
                CreatedAt = DateTime.UtcNow,
                Questions = [.. questionsDto.Select(q => new ExamQuestion
                {
                    QuestionText = q.QuestionText,
                    CorrectAnswer = q.CorrectAnswer,
                    Mark = q.Mark,
                    OptionsJson = q.Options != null && q.Options.Count > 0
                        ? System.Text.Json.JsonSerializer.Serialize(q.Options) : null,
                    CreatedAt = DateTime.UtcNow
                })]
            };

            context.Exams.Add(exam);
            await context.SaveChangesAsync();

            return MapToDto(exam);
        }

        public async Task<ExamDto> UploadFileExamAsync(Ulid subjectOfferingId, Microsoft.AspNetCore.Http.IFormFile file, Ulid doctorId)
        {
            var offering = await context.Set<SubjectOffering>()
                .AsNoTracking()
                .Include(so => so.Subject)
                .FirstOrDefaultAsync(so => so.Id == subjectOfferingId)
                ?? throw new KeyNotFoundException($"SubjectOffering {subjectOfferingId} not found.");

            if (offering.DoctorId != doctorId)
                throw new UnauthorizedAccessException("You are not the instructor for this offering.");

            if (file == null || file.Length == 0)
                throw new ArgumentException("File is empty or not provided.");

            // Normalize content type — browsers often send PDFs as application/octet-stream
            var ext = System.IO.Path.GetExtension(file.FileName).ToLowerInvariant();
            var contentType = file.ContentType;
            if (contentType == "application/octet-stream" || string.IsNullOrWhiteSpace(contentType))
            {
                contentType = ext switch
                {
                    ".pdf"  => "application/pdf",
                    ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                    ".doc"  => "application/msword",
                    _       => contentType
                };
            }

            using var stream = file.OpenReadStream();
            var fileStatus = await _fileService.UploadFileStreamAsync(doctorId, stream, file.FileName, contentType, file.Length);

            var exam = new Exam
            {
                Code = await GenerateUniqueExamCodeAsync(),
                Title = $"{offering.Subject.Name} - File Based Exam",
                Type = ExamType.Final,
                TotalMarks = 100,
                StartTime = DateTime.UtcNow.AddDays(1),
                EndTime = DateTime.UtcNow.AddDays(1).AddHours(2),
                Status = ExamStatus.Draft,
                Mode = ExamMode.File,
                FilePath = file.FileName,
                CreatedByDoctorId = doctorId,
                SubjectOfferingId = subjectOfferingId,
                CreatedAt = DateTime.UtcNow
            };

            context.Exams.Add(exam);
            await context.SaveChangesAsync();

            return MapToDto(exam);
        }
    }
}
