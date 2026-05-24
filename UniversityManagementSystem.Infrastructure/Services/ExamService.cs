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
            // 1. Validate Exam exists & is active — include Questions for immediate grading
            var exam = await context.Exams
                .Include(e => e.Questions)
                .Include(e => e.SubjectOffering)
                .FirstOrDefaultAsync(e => e.Id == examId)
                ?? throw new KeyNotFoundException($"Exam with ID {examId} not found.");

            if (exam.Status != ExamStatus.Published)
                throw new UnauthorizedAccessException("Exam is not published.");

            var currentTime = DateTime.UtcNow;

            if (currentTime < exam.StartTime)
                throw new InvalidOperationException($"Exam has not started yet. Starts at {exam.StartTime:HH:mm} UTC.");

            // Check end time — allow late submission if configured
            var effectiveEndTime = exam.AllowLateSubmission
                ? exam.EndTime.AddMinutes(exam.LateSubmissionWindowMinutes)
                : exam.EndTime;

            if (currentTime > effectiveEndTime)
                throw new InvalidOperationException("Exam time has ended. Submissions are no longer accepted.");

            // 2. Validate Student Enrollment (Active)
            var isEnrolled = await context.Enrollments
                .AsNoTracking()
                .AnyAsync(e => e.StudentId == studentId && e.SubjectOfferingId == exam.SubjectOfferingId && e.IsActive);

            if (!isEnrolled)
                throw new UnauthorizedAccessException("You are not actively enrolled in this course.");

            // 3. Check for previous submission — handle draft (save-progress) records
            var existingSubmission = await context.ExamSubmissions
                .FirstOrDefaultAsync(s => s.ExamId == examId && s.StudentId == studentId);

            if (existingSubmission != null && existingSubmission.IsCompleted)
                throw new InvalidOperationException("You have already submitted this exam.");

            var answersJson = System.Text.Json.JsonSerializer.Serialize(submissionDto.Answers);

            ExamSubmission submission;
            if (existingSubmission != null)
            {
                // Upgrade the in-progress draft to a completed submission
                existingSubmission.AnswersJson    = answersJson;
                existingSubmission.SubmittedAt    = DateTime.UtcNow;
                existingSubmission.IsCompleted    = true;
                existingSubmission.DraftAnswersJson = null;
                existingSubmission.LastSavedAt    = DateTime.UtcNow;
                submission = existingSubmission;
            }
            else
            {
                // 4. Create fresh submission
                submission = new ExamSubmission
                {
                    ExamId      = examId,
                    StudentId   = studentId,
                    SubmittedAt = DateTime.UtcNow,
                    IsCompleted = true,
                    IsGraded    = false,
                    Score       = null,
                    AnswersJson = answersJson
                };
                context.ExamSubmissions.Add(submission);
            }

            // Auto-grade MCQ / TrueFalse immediately
            _AutoGradeMcq(exam, submission);

            try
            {
                await context.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
            {
                if (ex.InnerException != null && ex.InnerException.Message.Contains("IX_ExamSubmissions_ExamId_StudentId"))
                    throw new InvalidOperationException("You have already submitted this exam.");
                throw;
            }

            return submission.Id;
        }

        /// <summary>
        /// Resolves q.CorrectAnswer to the actual option text regardless of how it was stored:
        ///   "0"/"1"/"2"/"3" (index) → options[n]
        ///   "A"/"B"/"C"/"D" (letter) → options[n]
        ///   anything else   → treated as the literal correct text already
        /// </summary>
        private static string ResolveCorrectText(ExamQuestion q)
        {
            var raw = q.CorrectAnswer?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(raw)) return raw;

            List<string>? options = null;
            if (!string.IsNullOrEmpty(q.OptionsJson))
            {
                try { options = System.Text.Json.JsonSerializer.Deserialize<List<string>>(q.OptionsJson); }
                catch { }
            }

            if (options == null || options.Count == 0) return raw;

            // numeric index: "0", "1", "2", "3"
            if (int.TryParse(raw, out int idx) && idx >= 0 && idx < options.Count)
                return options[idx];

            // letter: "A", "B", "C", "D"
            if (raw.Length == 1)
            {
                int letterIdx = char.ToUpperInvariant(raw[0]) - 'A';
                if (letterIdx >= 0 && letterIdx < options.Count)
                    return options[letterIdx];
            }

            return raw; // already the option text
        }

        /// <summary>Grades all MCQ/TrueFalse answers in a submission immediately. Mutates submission in-place.</summary>
        private static void _AutoGradeMcq(Exam exam, ExamSubmission submission)
        {
            List<ExamAnswerDto> answers;
            try { answers = System.Text.Json.JsonSerializer.Deserialize<List<ExamAnswerDto>>(submission.AnswersJson) ?? new(); }
            catch { answers = new(); }

            bool hasEssay = exam.Questions.Any(q =>
                q.QuestionType == QuestionType.Essay || q.QuestionType == QuestionType.ShortAnswer);

            double score = 0;
            foreach (var q in exam.Questions.Where(q => q.DeletedAt == null))
            {
                if (q.QuestionType != QuestionType.MCQ && q.QuestionType != QuestionType.TrueFalse)
                    continue;
                var ans = answers.FirstOrDefault(a => a.QuestionId == q.Id);
                var correctText = ResolveCorrectText(q);
                if (ans != null &&
                    string.Equals(ans.AnswerText?.Trim(), correctText, StringComparison.OrdinalIgnoreCase))
                    score += q.Mark;
            }

            submission.Score    = score;
            submission.IsGraded = !hasEssay;
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
            var durationMinutes = dto.DurationMinutes > 0
                ? dto.DurationMinutes
                : (int)(dto.EndTime - dto.StartTime).TotalMinutes;

            var exam = new Exam
            {
                Code = await GenerateUniqueExamCodeAsync(),
                Title = dto.Title,
                Instructions = dto.Instructions,
                Type = dto.Type,
                TotalMarks = dto.Questions.Sum(q => q.Mark),
                StartTime = dto.StartTime,
                EndTime = dto.EndTime,
                DurationMinutes = durationMinutes,
                Status = dto.Status,
                Mode = ExamMode.Structured,
                CreatedByDoctorId = doctorId,
                SubjectOfferingId = subjectOfferingId,
                IsRandomized = dto.IsRandomized,
                QuestionsPerStudent = dto.QuestionsPerStudent,
                AllowLateSubmission = dto.AllowLateSubmission,
                LateSubmissionWindowMinutes = dto.LateSubmissionWindowMinutes,
                CreatedAt = DateTime.UtcNow,
                Questions = [.. dto.Questions.Select(q => new ExamQuestion
                {
                    QuestionText = q.QuestionText,
                    CorrectAnswer = q.CorrectAnswer,
                    Mark = q.Mark,
                    QuestionType = (QuestionType)q.QuestionType,
                    OptionsJson = q.Options != null && q.Options.Count > 0
                        ? System.Text.Json.JsonSerializer.Serialize(q.Options) : null,
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

            var duration = exam.DurationMinutes > 0
                ? exam.DurationMinutes
                : (int)(exam.EndTime - exam.StartTime).TotalMinutes;

            return new ExamDto
            {
                Id = exam.Id,
                Code = exam.Code,
                Title = exam.Title,
                Instructions = exam.Instructions,
                Type = exam.Type.ToString(),
                TotalMarks = exam.TotalMarks,
                StartTime = exam.StartTime,
                EndTime = exam.EndTime,
                DurationMinutes = duration,
                Mode = exam.Mode.ToString(),
                Status = exam.Status.ToString(),
                FilePath = exam.FilePath,
                CreatedByDoctorId = exam.CreatedByDoctorId,
                SubjectOfferingId = exam.SubjectOfferingId,
                SubjectName = exam.SubjectOffering?.Subject?.Name ?? string.Empty,
                IsRandomized = exam.IsRandomized,
                QuestionsPerStudent = exam.QuestionsPerStudent,
                AllowLateSubmission = exam.AllowLateSubmission,
                LateSubmissionWindowMinutes = exam.LateSubmissionWindowMinutes,
                QuestionCount = questions.Count,
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
                .Include(e => e.Questions)
                .Include(e => e.SubjectOffering)
                .FirstOrDefaultAsync(e => e.Id == examId)
                ?? throw new KeyNotFoundException($"Exam with ID {examId} not found.");

            if (exam.SubjectOffering.DoctorId != doctorId)
                throw new UnauthorizedAccessException("You are not authorized to view results for this exam.");

            var submissions = await context.ExamSubmissions
                .AsNoTracking()
                .Include(s => s.Student)
                .Where(s => s.ExamId == examId && s.IsCompleted)
                .ToListAsync();

            var questions = exam.Questions.Where(q => q.DeletedAt == null).ToList();
            int mcqCount = questions.Count(q => q.QuestionType == QuestionType.MCQ || q.QuestionType == QuestionType.TrueFalse);
            int essayCount = questions.Count(q => q.QuestionType == QuestionType.Essay || q.QuestionType == QuestionType.ShortAnswer);

            return submissions.Select(s =>
            {
                double? pct = s.Score.HasValue && exam.TotalMarks > 0
                    ? Math.Round(s.Score.Value / exam.TotalMarks * 100, 1) : null;

                return new ExamSubmissionResponseDto
                {
                    Id                   = s.Id,
                    ExamId               = s.ExamId,
                    StudentId            = s.StudentId,
                    StudentName          = s.Student.FullName,
                    StudentCode          = s.Student.Code,
                    SubmittedAt          = s.SubmittedAt,
                    DurationSpentMinutes = null,   // no StartedAt on entity
                    Score                = s.Score,
                    TotalMarks           = exam.TotalMarks,
                    Percentage           = pct,
                    IsGraded             = s.IsGraded,
                    IsCompleted          = s.IsCompleted,
                    IsFlagged            = false,
                    NeedsManualGrading   = s.IsCompleted && !s.IsGraded,
                    AutoGradedMcqCount   = mcqCount,
                    PendingEssayCount    = s.IsGraded ? 0 : essayCount,
                };
            });
        }

        public async Task<SubmissionDetailDto> GetSubmissionDetailAsync(Ulid submissionId, Ulid doctorId)
        {
            var submission = await context.ExamSubmissions
                .AsNoTracking()
                .Include(s => s.Student)
                .Include(s => s.Exam)
                    .ThenInclude(e => e.Questions)
                .Include(s => s.Exam)
                    .ThenInclude(e => e.SubjectOffering)
                .FirstOrDefaultAsync(s => s.Id == submissionId)
                ?? throw new KeyNotFoundException($"Submission {submissionId} not found.");

            if (submission.Exam.SubjectOffering.DoctorId != doctorId)
                throw new UnauthorizedAccessException("You are not authorized to view this submission.");

            List<ExamAnswerDto> studentAnswers;
            try { studentAnswers = System.Text.Json.JsonSerializer.Deserialize<List<ExamAnswerDto>>(submission.AnswersJson) ?? new(); }
            catch { studentAnswers = new(); }

            var answerDetails = submission.Exam.Questions
                .Where(q => q.DeletedAt == null)
                .Select(q =>
                {
                    var options = q.OptionsJson != null
                        ? System.Text.Json.JsonSerializer.Deserialize<List<string>>(q.OptionsJson)
                        : null;

                    int? correctIndex = options != null
                        ? options.FindIndex(o => string.Equals(o, q.CorrectAnswer, StringComparison.OrdinalIgnoreCase))
                        : null;
                    if (correctIndex < 0) correctIndex = null;

                    var studentAns = studentAnswers.FirstOrDefault(a => a.QuestionId == q.Id);
                    string? studentText = studentAns?.AnswerText;

                    int? studentIndex = null;
                    bool? isCorrect = null;
                    double awarded = 0;

                    if (q.QuestionType == QuestionType.MCQ || q.QuestionType == QuestionType.TrueFalse)
                    {
                        if (options != null && !string.IsNullOrEmpty(studentText))
                            studentIndex = options.FindIndex(o => string.Equals(o, studentText, StringComparison.OrdinalIgnoreCase));
                        if (studentIndex < 0) studentIndex = null;

                        var correctText2 = ResolveCorrectText(q);
                        isCorrect = !string.IsNullOrEmpty(studentText) &&
                            string.Equals(studentText.Trim(), correctText2, StringComparison.OrdinalIgnoreCase);
                        awarded = isCorrect == true ? q.Mark : 0;
                    }
                    // Essay — awarded computed separately via GradeQuestion

                    return new SubmissionAnswerDetailDto
                    {
                        QuestionId         = q.Id,
                        QuestionText       = q.QuestionText,
                        QuestionType       = q.QuestionType.ToString(),
                        Marks              = q.Mark,
                        Options            = options,
                        CorrectIndex       = correctIndex,
                        StudentAnswerIndex = studentIndex,
                        StudentAnswerText  = studentText,
                        IsCorrect          = isCorrect,
                        AwardedScore       = awarded,
                        ProfessorComment   = null,
                    };
                }).ToList();

            return new SubmissionDetailDto
            {
                SubmissionId = submission.Id,
                StudentName  = submission.Student.FullName,
                StudentCode  = submission.Student.Code,
                SubmittedAt  = submission.SubmittedAt,
                Score        = submission.Score,
                TotalMarks   = submission.Exam.TotalMarks,
                IsGraded     = submission.IsGraded,
                Answers      = answerDetails,
            };
        }

        public async Task GradeQuestionAsync(Ulid submissionId, GradeQuestionDto dto, Ulid doctorId)
        {
            var submission = await context.ExamSubmissions
                .Include(s => s.Exam)
                    .ThenInclude(e => e.Questions)
                .Include(s => s.Exam)
                    .ThenInclude(e => e.SubjectOffering)
                .FirstOrDefaultAsync(s => s.Id == submissionId)
                ?? throw new KeyNotFoundException($"Submission {submissionId} not found.");

            if (submission.Exam.SubjectOffering.DoctorId != doctorId)
                throw new UnauthorizedAccessException("You are not authorized to grade this submission.");

            var question = submission.Exam.Questions.FirstOrDefault(q => q.Id == dto.QuestionId)
                ?? throw new KeyNotFoundException($"Question {dto.QuestionId} not found in exam.");

            if (dto.Score < 0 || dto.Score > question.Mark)
                throw new InvalidOperationException($"Score must be between 0 and {question.Mark}.");

            // Rebuild answers list with updated score for this question stored in a side-table
            // Since ExamSubmission has no per-question scores table, we keep a GradingJson
            // We store per-question grades in a simple JSON dict on the submission
            var gradingDict = new System.Collections.Generic.Dictionary<string, double>();
            if (!string.IsNullOrEmpty(submission.GradingJson))
            {
                try { gradingDict = System.Text.Json.JsonSerializer.Deserialize<System.Collections.Generic.Dictionary<string, double>>(submission.GradingJson) ?? new(); }
                catch { }
            }
            gradingDict[dto.QuestionId.ToString()] = dto.Score;
            submission.GradingJson = System.Text.Json.JsonSerializer.Serialize(gradingDict);

            // Recompute total score: MCQ/TF auto-graded + manually graded essays
            List<ExamAnswerDto> studentAnswers;
            try { studentAnswers = System.Text.Json.JsonSerializer.Deserialize<List<ExamAnswerDto>>(submission.AnswersJson) ?? new(); }
            catch { studentAnswers = new(); }

            double total = 0;
            bool anyEssayPending = false;
            foreach (var q in submission.Exam.Questions.Where(q => q.DeletedAt == null))
            {
                if (q.QuestionType == QuestionType.MCQ || q.QuestionType == QuestionType.TrueFalse)
                {
                    var ans = studentAnswers.FirstOrDefault(a => a.QuestionId == q.Id);
                    if (ans != null && string.Equals(ans.AnswerText?.Trim(), ResolveCorrectText(q), StringComparison.OrdinalIgnoreCase))
                        total += q.Mark;
                }
                else // Essay / ShortAnswer
                {
                    if (gradingDict.TryGetValue(q.Id.ToString(), out var essayScore))
                        total += essayScore;
                    else
                        anyEssayPending = true;
                }
            }

            submission.Score = total;
            submission.IsGraded = !anyEssayPending;

            await context.SaveChangesAsync();
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

        public async Task<StudentSubmissionResultDto?> GetStudentSubmissionAsync(Ulid examId, Ulid studentId)
        {
            var submission = await context.ExamSubmissions
                .AsNoTracking()
                .Include(s => s.Exam).ThenInclude(e => e.Questions)
                .FirstOrDefaultAsync(s => s.ExamId == examId && s.StudentId == studentId);

            if (submission == null) return null;

            List<ExamAnswerDto> studentAnswers;
            try { studentAnswers = System.Text.Json.JsonSerializer.Deserialize<List<ExamAnswerDto>>(submission.AnswersJson) ?? new(); }
            catch { studentAnswers = new(); }

            var answerResults = submission.Exam.Questions
                .Where(q => q.DeletedAt == null)
                .Select(q =>
                {
                    var ans = studentAnswers.FirstOrDefault(a => a.QuestionId == q.Id);
                    var text = ans?.AnswerText ?? string.Empty;
                    bool? isCorrect = null;
                    double earned = 0;

                    if (q.QuestionType == QuestionType.MCQ || q.QuestionType == QuestionType.TrueFalse)
                    {
                        var correctText = ResolveCorrectText(q);
                        isCorrect = !string.IsNullOrEmpty(text) &&
                            string.Equals(text.Trim(), correctText, StringComparison.OrdinalIgnoreCase);
                        earned = isCorrect == true ? q.Mark : 0;
                    }

                    return new StudentAnswerResultDto
                    {
                        QuestionId  = q.Id,
                        AnswerText  = text,
                        IsCorrect   = isCorrect,
                        EarnedMarks = earned,
                    };
                }).ToList();

            double totalMarks = submission.Exam.TotalMarks;
            double? pct = submission.Score.HasValue && totalMarks > 0
                ? Math.Round(submission.Score.Value / totalMarks * 100, 1) : null;

            return new StudentSubmissionResultDto
            {
                SubmissionId = submission.Id,
                TotalScore   = submission.Score,
                TotalMarks   = totalMarks,
                Percentage   = pct,
                IsGraded     = submission.IsGraded,
                SubmittedAt  = submission.SubmittedAt,
                Answers      = answerResults,
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
            bool hasEssayOrShortAnswer = exam.Questions.Any(q =>
                q.QuestionType == QuestionType.Essay || q.QuestionType == QuestionType.ShortAnswer);

            // 2. Loop through COMPLETED submissions only
            foreach (var submission in exam.Submissions.Where(s => s.IsCompleted))
            {
                var answers = System.Text.Json.JsonSerializer.Deserialize<List<ExamAnswerDto>>(submission.AnswersJson);

                if (answers == null || answers.Count == 0)
                {
                    submission.Score    = 0;
                    submission.IsGraded = !hasEssayOrShortAnswer; // only mark graded if no manual questions
                    gradedCount++;
                    continue;
                }

                double totalScore    = 0;
                bool   needsManual   = false;

                foreach (var question in exam.Questions.Where(q => q.DeletedAt == null))
                {
                    var studentAnswer = answers.FirstOrDefault(a => a.QuestionId == question.Id);
                    if (studentAnswer == null) continue;

                    switch (question.QuestionType)
                    {
                        case QuestionType.MCQ:
                        case QuestionType.TrueFalse:
                            // Exact match (case-insensitive)
                            if (!string.IsNullOrEmpty(studentAnswer.AnswerText) &&
                                string.Equals(studentAnswer.AnswerText.Trim(),
                                              ResolveCorrectText(question),
                                              StringComparison.OrdinalIgnoreCase))
                                totalScore += question.Mark;
                            break;

                        case QuestionType.Essay:
                        case QuestionType.ShortAnswer:
                            // Cannot auto-grade — flag for manual review
                            needsManual = true;
                            break;
                    }
                }

                submission.Score    = totalScore;
                submission.IsGraded = !needsManual; // fully graded only if no essay/short-answer
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

            var oldValues = System.Text.Json.JsonSerializer.Serialize(new { exam.Title, exam.DeletedAt });

            // Delete all submissions first, then soft-delete the exam
            if (exam.Submissions.Any())
            {
                foreach (var sub in exam.Submissions)
                    sub.DeletedAt = DateTime.UtcNow;
            }

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

            // Use pre-generated questions from FastAPI if provided (avoids double AI call).
            List<CreateExamQuestionDto> questionsDto;
            if (request.PreGeneratedQuestions != null && request.PreGeneratedQuestions.Count > 0)
            {
                questionsDto = request.PreGeneratedQuestions;
            }
            else
            {
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
                questionsDto = await _aiService.GenerateExamAsync(structuredRequest);
            }

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

        // ── Save Progress (auto-save draft during exam) ──────────────────────
        public async Task<SaveProgressResponseDto> SaveProgressAsync(Ulid examId, Ulid studentId, SaveProgressDto dto)
        {
            var submission = await context.ExamSubmissions
                .FirstOrDefaultAsync(s => s.ExamId == examId && s.StudentId == studentId);

            if (submission == null)
            {
                submission = new ExamSubmission
                {
                    ExamId = examId,
                    StudentId = studentId,
                    IsCompleted = false,
                    DraftAnswersJson = System.Text.Json.JsonSerializer.Serialize(dto.Answers),
                    LastSavedAt = DateTime.UtcNow,
                    SubmittedAt = DateTime.UtcNow
                };
                context.ExamSubmissions.Add(submission);
            }
            else
            {
                if (submission.IsCompleted)
                    throw new InvalidOperationException("Exam already submitted — cannot save progress.");

                submission.DraftAnswersJson = System.Text.Json.JsonSerializer.Serialize(dto.Answers);
                submission.LastSavedAt = DateTime.UtcNow;
            }

            await context.SaveChangesAsync();
            return new SaveProgressResponseDto
            {
                SavedAt = DateTime.UtcNow,
                AnswersSaved = dto.Answers.Count,
                Message = "Progress saved successfully."
            };
        }

        // ── Get Exam Session (student opens exam) ────────────────────────────
        public async Task<ExamSessionDto> GetExamSessionAsync(Ulid examId, Ulid studentId)
        {
            var exam = await context.Exams
                .AsNoTracking()
                .Include(e => e.Questions)
                .Include(e => e.SubjectOffering).ThenInclude(so => so.Subject)
                .FirstOrDefaultAsync(e => e.Id == examId)
                ?? throw new KeyNotFoundException("Exam not found.");

            var isEnrolled = await context.Enrollments
                .AsNoTracking()
                .AnyAsync(e => e.StudentId == studentId && e.SubjectOfferingId == exam.SubjectOfferingId && e.IsActive);

            if (!isEnrolled)
                throw new UnauthorizedAccessException("You are not enrolled in this subject.");

            if (exam.Status != ExamStatus.Published)
                throw new UnauthorizedAccessException("Exam is not available yet.");

            var submission = await context.ExamSubmissions
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.ExamId == examId && s.StudentId == studentId);

            var now = DateTime.UtcNow;
            var duration = exam.DurationMinutes > 0
                ? exam.DurationMinutes
                : (int)(exam.EndTime - exam.StartTime).TotalMinutes;
            var secondsRemaining = (int)Math.Max(0, (exam.EndTime - now).TotalSeconds);

            var draftAnswers = new List<ExamAnswerDto>();
            if (submission?.DraftAnswersJson != null && !submission.IsCompleted)
            {
                try { draftAnswers = System.Text.Json.JsonSerializer.Deserialize<List<ExamAnswerDto>>(submission.DraftAnswersJson) ?? new(); }
                catch { }
            }

            var questions = exam.Questions
                .Where(q => q.DeletedAt == null)
                .Select(q => new ExamQuestionDto
                {
                    Id = q.Id,
                    QuestionText = q.QuestionText,
                    Options = q.OptionsJson != null
                        ? System.Text.Json.JsonSerializer.Deserialize<List<string>>(q.OptionsJson) : null,
                    QuestionType = q.QuestionType.ToString(),
                    Mark = q.Mark
                }).ToList();

            return new ExamSessionDto
            {
                ExamId = exam.Id,
                ExamCode = exam.Code,
                Title = exam.Title,
                Instructions = exam.Instructions,
                DurationMinutes = duration,
                StartTime = exam.StartTime,
                EndTime = exam.EndTime,
                IsSubmitted = submission?.IsCompleted ?? false,
                SubmittedAt = submission?.IsCompleted == true ? submission.SubmittedAt : null,
                DraftAnswers = draftAnswers,
                Questions = questions,
                SecondsRemaining = secondsRemaining
            };
        }

        // ── Analytics ────────────────────────────────────────────────────────
        public async Task<ExamAnalyticsDto> GetExamAnalyticsAsync(Ulid examId, Ulid doctorId)
        {
            var exam = await context.Exams
                .AsNoTracking()
                .Include(e => e.Questions)
                .Include(e => e.SubjectOffering)
                .FirstOrDefaultAsync(e => e.Id == examId)
                ?? throw new KeyNotFoundException("Exam not found.");

            if (exam.SubjectOffering.DoctorId != doctorId)
                throw new UnauthorizedAccessException("Not authorized to view this exam's analytics.");

            var submissions = await context.ExamSubmissions
                .AsNoTracking()
                .Where(s => s.ExamId == examId && s.IsCompleted)
                .ToListAsync();

            var enrolledCount = await context.Enrollments
                .AsNoTracking()
                .CountAsync(e => e.SubjectOfferingId == exam.SubjectOfferingId && e.IsActive);

            var gradedSubmissions = submissions.Where(s => s.IsGraded && s.Score.HasValue).ToList();
            var passThreshold = exam.TotalMarks * 0.5;

            var questionStats = new List<QuestionAnalyticsDto>();
            foreach (var q in exam.Questions.Where(q => q.DeletedAt == null))
            {
                int answered = 0, correct = 0;
                foreach (var sub in submissions)
                {
                    try
                    {
                        var answers = System.Text.Json.JsonSerializer.Deserialize<List<ExamAnswerDto>>(sub.AnswersJson);
                        var ans = answers?.FirstOrDefault(a => a.QuestionId == q.Id);
                        if (ans != null)
                        {
                            answered++;
                            if (ans.AnswerText?.Trim().Equals(ResolveCorrectText(q), StringComparison.OrdinalIgnoreCase) == true)
                                correct++;
                        }
                    }
                    catch { }
                }
                questionStats.Add(new QuestionAnalyticsDto
                {
                    QuestionId = q.Id,
                    QuestionText = q.QuestionText,
                    TotalAnswered = answered,
                    TotalCorrect = correct,
                    CorrectRate = answered > 0 ? Math.Round((double)correct / answered * 100, 1) : 0
                });
            }

            return new ExamAnalyticsDto
            {
                ExamId = exam.Id,
                ExamTitle = exam.Title,
                TotalEnrolled = enrolledCount,
                TotalSubmitted = submissions.Count,
                TotalGraded = gradedSubmissions.Count,
                TotalPassed = gradedSubmissions.Count(s => s.Score >= passThreshold),
                TotalFailed = gradedSubmissions.Count(s => s.Score < passThreshold),
                AverageScore = gradedSubmissions.Count > 0 ? Math.Round(gradedSubmissions.Average(s => s.Score!.Value), 2) : 0,
                HighestScore = gradedSubmissions.Count > 0 ? gradedSubmissions.Max(s => s.Score!.Value) : 0,
                LowestScore = gradedSubmissions.Count > 0 ? gradedSubmissions.Min(s => s.Score!.Value) : 0,
                PassRate = gradedSubmissions.Count > 0 ? Math.Round((double)gradedSubmissions.Count(s => s.Score >= passThreshold) / gradedSubmissions.Count * 100, 1) : 0,
                QuestionStats = questionStats
            };
        }
    }
}
