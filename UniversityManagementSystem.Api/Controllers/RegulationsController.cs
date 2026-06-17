using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using NUlid;
using UniversityManagementSystem.Core.DTOs;
using UniversityManagementSystem.Core.Entities;
using UniversityManagementSystem.Core.Interfaces;
using UniversityManagementSystem.Infrastructure.Data;

namespace UniversityManagementSystem.Api.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class RegulationsController(
        IRegulationService service,
        IDistributedCache cache,
        AppDbContext context,
        IStorageService storageService,
        IFileService fileService,
        IUserContextService userContext) : ControllerBase
    {
        private readonly IRegulationService _service = service;
        private readonly IDistributedCache _cache = cache;
        private readonly AppDbContext _context = context;
        private readonly IStorageService _storageService = storageService;
        private readonly IFileService _fileService = fileService;
        private readonly IUserContextService _userContext = userContext;
        private const string CacheKey = "Regulations_All";

        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new UniversityManagementSystem.Api.Converters.UlidJsonConverter() }
        };

        // Allowed file MIME types for regulation attachments
        private static readonly HashSet<string> AllowedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "application/pdf",
            "application/msword",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            "application/vnd.ms-excel",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "text/plain"
        };
        private const long MaxFileSizeBytes = 50 * 1024 * 1024; // 50 MB

        // ── Helper: build RegulationDto with public URL ───────────────────────
        private Task<RegulationDto> ToDto(Regulation r)
        {
            string? fileUrl = null;
            if (r.FileId.HasValue && r.File != null)
                fileUrl = _storageService.BuildUrl(r.File.StorageKey);

            return Task.FromResult(new RegulationDto
            {
                Id = r.Id,
                Title = r.Title,
                Content = r.Content,
                Type = r.Type.ToString(),
                IsActive = r.IsActive,
                FileId = r.FileId?.ToString(),
                FileUrl = fileUrl,
                DepartmentId = r.DepartmentId?.ToString(),
                Subjects = r.RegulationSubjects?.Select(rs => new RegulationSubjectDto
                {
                    SubjectId = rs.SubjectId,
                    Semester = rs.Semester,
                    IsRequired = rs.IsRequired
                }).ToList() ?? new List<RegulationSubjectDto>()
            });
        }

        // ── GET /api/Regulations ─────────────────────────────────────────────
        [HttpGet]
        [AllowAnonymous]
        public async Task<ActionResult<IEnumerable<RegulationDto>>> GetAll()
        {
            // Task.WhenAny guarantees the 500ms timeout even when StackExchange.Redis
            // ignores CancellationToken and waits for the full TCP connection timeout (~11s).
            try
            {
                var cacheTask   = _cache.GetStringAsync(CacheKey);
                var timeoutTask = Task.Delay(500);
                if (await Task.WhenAny(cacheTask, timeoutTask) == cacheTask)
                {
                    var cached = await cacheTask;
                    if (!string.IsNullOrEmpty(cached))
                    {
                        try { return Ok(JsonSerializer.Deserialize<IEnumerable<RegulationDto>>(cached, _jsonOptions)); }
                        catch { try { _ = _cache.RemoveAsync(CacheKey); } catch { } }
                    }
                }
                // else: timeout won — fall through to DB without waiting for Redis
            }
            catch { /* cache unavailable — fall through to DB */ }

            var list = await _service.GetAllAsync();
            var dtos = new List<RegulationDto>();
            foreach (var r in list)
                dtos.Add(await ToDto(r));

            // Fire-and-forget cache write — never block the response on it
            _ = Task.Run(async () =>
            {
                try
                {
                    using var writeCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(800));
                    await _cache.SetStringAsync(CacheKey, JsonSerializer.Serialize(dtos, _jsonOptions),
                        new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5) },
                        writeCts.Token);
                }
                catch { /* ignore cache write failures */ }
            });

            return Ok(dtos);
        }

        // ── GET /api/Regulations/active ──────────────────────────────────────
        [HttpGet("active")]
        [AllowAnonymous]
        public async Task<ActionResult<IEnumerable<RegulationDto>>> GetActive()
        {
            var list = await _service.GetActiveAsync();
            var dtos = new List<RegulationDto>();
            foreach (var r in list)
                dtos.Add(await ToDto(r));
            return Ok(dtos);
        }

        // ── GET /api/Regulations/by-code/{code} ──────────────────────────────
        /// <summary>
        /// [PREFERRED] Retrieve a regulation by its auto-generated slug code.
        /// Code is derived from Title, e.g. "General Rules" → "general-rules".
        /// </summary>
        [HttpGet("by-code/{code}")]
        public async Task<IActionResult> GetByCode(string code)
        {
            var regulation = await _service.GetByCodeAsync(code);
            if (regulation == null)
                return NotFound($"Regulation with code '{code}' not found.");

            return Ok(await ToDto(regulation));
        }

        // ── GET /api/Regulations/by-department/{departmentId} ────────────────
        [HttpGet("by-department/{departmentId}")]
        public async Task<IActionResult> GetByDepartment(string departmentId)
        {
            if (!Ulid.TryParse(departmentId, out var deptId))
                return BadRequest("Invalid Department ID.");

            var regulations = await _service.GetByDepartmentAsync(deptId);
            var dtos = new List<RegulationDto>();
            foreach (var r in regulations)
                dtos.Add(await ToDto(r));

            return Ok(dtos);
        }

        // ── GET /api/Regulations/student/{studentId} ─────────────────────────
        [HttpGet("student/{studentId}")]
        public async Task<IActionResult> GetForStudent(string studentId)
        {
            if (!Ulid.TryParse(studentId, out var stuId))
                return BadRequest("Invalid Student ID.");

            var regulation = await _service.GetForStudentAsync(stuId);
            if (regulation == null)
                return NotFound("Student has no regulation assigned.");

            return Ok(await ToDto(regulation));
        }

        // ── GET /api/Regulations/my-roadmap ─────────────────────────────────
        /// <summary>
        /// Returns the authenticated student's dynamic academic roadmap.
        /// Built from REAL enrollments semester-by-semester (not static regulation).
        /// Each semester shows courses, activities, grades, GPA, and retake status.
        /// </summary>
        [HttpGet("my-roadmap")]
        [Authorize(Roles = "Student,SuperAdmin")]
        public async Task<IActionResult> GetMyRoadmap()
        {
            var profileIdClaim = User.FindFirst("ProfileId");
            if (profileIdClaim == null) return Unauthorized("Student profile not found.");
            if (!Ulid.TryParse(profileIdClaim.Value, out var studentId))
                return Unauthorized("Invalid student ID.");

            // 1. Student
            var student = await _context.Students
                .AsNoTracking()
                .Include(s => s.Department).ThenInclude(d => d!.College)
                .Include(s => s.Batch)
                .FirstOrDefaultAsync(s => s.Id == studentId);

            if (student == null) return NotFound("Student not found.");

            // 2. Regulation (optional — provides curriculum context + upcoming subjects)
            Regulation? regulation = null;
            var regSubjects = new List<RegulationSubject>();
            if (student.RegulationId.HasValue)
            {
                regulation = await _context.Regulations
                    .AsNoTracking()
                    .Include(r => r.RegulationSubjects).ThenInclude(rs => rs.Subject)
                    .FirstOrDefaultAsync(r => r.Id == student.RegulationId.Value);
                regSubjects = regulation?.RegulationSubjects
                    .Where(rs => rs.Subject != null)
                    .ToList() ?? [];
            }

            // 3. All enrollments — include withdrawn (DeletedAt != null) for full history
            // NOTE: EF Core DbContext is NOT thread-safe — all queries sequential.
            var enrollments = await _context.Enrollments
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Include(e => e.SubjectOffering).ThenInclude(so => so.Semester).ThenInclude(s => s.AcademicYear)
                .Include(e => e.SubjectOffering).ThenInclude(so => so.Subject)
                .Where(e => e.StudentId == studentId
                         && e.SubjectOffering != null
                         && e.SubjectOffering.Semester != null
                         && e.SubjectOffering.Subject != null)
                .ToListAsync();

            // 4. Regulation totals (for graduation progress)
            int totalRegCreditHours = regSubjects.Sum(rs => rs.Subject?.CreditHours ?? 0);
            int totalRegSubjects    = regSubjects.Count;

            // No enrollments yet — return skeleton roadmap
            if (!enrollments.Any())
            {
                var upcoming0 = regSubjects
                    .OrderBy(rs => rs.Semester)
                    .Select(rs => new SubjectStatusDto
                    {
                        SubjectId   = rs.SubjectId.ToString(),
                        SubjectName = rs.Subject!.Name,
                        SubjectCode = rs.Subject.Code,
                        CreditHours = rs.Subject.CreditHours,
                        IsRequired  = rs.IsRequired,
                        Status      = "upcoming",
                    }).ToList();

                return Ok(new AcademicRoadmapDto
                {
                    StudentId            = student.Id.ToString(),
                    StudentName          = student.FullName,
                    StudentCode          = student.Code,
                    RegulationId         = regulation?.Id.ToString(),
                    RegulationTitle      = regulation?.Title,
                    DepartmentName       = student.Department?.Name ?? "",
                    CollegeName          = student.Department?.College?.Name ?? "",
                    BatchName            = student.Batch?.Name ?? "",
                    TotalSemesters       = 0,
                    TotalCreditHours     = totalRegCreditHours,
                    CompletedCreditHours = 0,
                    RemainingCreditHours = totalRegCreditHours,
                    TotalSubjects        = totalRegSubjects,
                    GraduationProgressPercent = 0,
                    RecommendedNext      = upcoming0,
                    Recommendations      = ["لا توجد تسجيلات بعد، يُنصح بالتسجيل في الفصل الأول"],
                });
            }

            var offeringIds = enrollments.Select(e => e.SubjectOfferingId).ToHashSet();

            // 5. Finalized grades indexed by SubjectOfferingId
            var gradesByOffering = await _context.StudentGrades
                .AsNoTracking()
                .Where(g => g.StudentId == studentId && g.IsFinalized
                         && offeringIds.Contains(g.SubjectOfferingId))
                .ToDictionaryAsync(g => g.SubjectOfferingId);

            // 6. Assignments + student submissions
            var assignments = await _context.Assignments
                .AsNoTracking()
                .Where(a => offeringIds.Contains(a.SubjectOfferingId))
                .ToListAsync();

            var assignmentIds = assignments.Select(a => a.Id).ToHashSet();
            var asnSubmissions = await _context.AssignmentSubmissions
                .AsNoTracking()
                .Where(s => s.StudentId == studentId && assignmentIds.Contains(s.AssignmentId))
                .ToDictionaryAsync(s => s.AssignmentId);

            // 7. Exams + student submissions
            var exams = await _context.Exams
                .AsNoTracking()
                .Where(e => offeringIds.Contains(e.SubjectOfferingId))
                .ToListAsync();

            var examIds = exams.Select(e => e.Id).ToHashSet();
            var examSubmissions = await _context.ExamSubmissions
                .AsNoTracking()
                .Where(s => s.StudentId == studentId && examIds.Contains(s.ExamId))
                .ToDictionaryAsync(s => s.ExamId);

            // 8. Group assignments/exams by offering for fast lookup
            var asnByOffering  = assignments.GroupBy(a => a.SubjectOfferingId)
                                            .ToDictionary(g => g.Key, g => g.ToList());
            var examByOffering = exams.GroupBy(e => e.SubjectOfferingId)
                                      .ToDictionary(g => g.Key, g => g.ToList());

            // 9. Retake tracking: ordered enrollment history per subject
            var historyBySubject = enrollments
                .GroupBy(e => e.SubjectOffering.SubjectId)
                .ToDictionary(g => g.Key,
                    g => g.OrderBy(e => e.SubjectOffering.Semester.StartDate).ToList());

            // 10. Build semesters — group by real Semester, ordered by StartDate
            var now = DateTime.UtcNow;
            var bySemester = enrollments
                .GroupBy(e => e.SubjectOffering.SemesterId)
                .OrderBy(g => g.First().SubjectOffering.Semester.StartDate)
                .ToList();

            double cumulativeGpaPoints  = 0;
            int    cumulativeCreditHrs  = 0;
            int    totalEarnedCredits   = 0;
            int    totalPassed          = 0;
            int    totalFailed          = 0;
            int    totalInProgress      = 0;
            int    semNumber            = 0;
            var    semesterDtos         = new List<SemesterRoadmapDto>();

            foreach (var semGroup in bySemester)
            {
                semNumber++;
                var sem         = semGroup.First().SubjectOffering.Semester;
                var acadYear    = sem.AcademicYear;
                string semStatus = sem.EndDate < now   ? "completed"
                                 : sem.StartDate <= now ? "in_progress"
                                 : "upcoming";

                double semGpaPoints = 0; int semCreditForGpa = 0;
                int semPassed = 0, semFailed = 0, semInProg = 0, semWithdrawn = 0;
                int semEarned = 0;
                var courses = new List<SubjectStatusDto>();

                foreach (var enr in semGroup)
                {
                    var offering = enr.SubjectOffering;
                    var subject  = offering.Subject;
                    gradesByOffering.TryGetValue(offering.Id, out var grade);

                    // Course status
                    string cStatus;
                    if (enr.DeletedAt != null)
                        cStatus = "withdrawn";
                    else if (grade != null)
                        cStatus = grade.GradePoints >= 1.0 ? "passed" : "failed";
                    else
                        cStatus = "in_progress";

                    // Retake detection
                    var history     = historyBySubject.GetValueOrDefault(subject.Id, []);
                    int idxInHist   = history.IndexOf(enr);
                    bool isRetake   = idxInHist > 0;
                    int  retakeCount = Math.Max(0, idxInHist);

                    // Is this subject required in the regulation?
                    bool isRequired = regSubjects.Any(rs => rs.SubjectId == subject.Id && rs.IsRequired);

                    // Activities
                    var activities = BuildActivities(
                        asnByOffering.GetValueOrDefault(offering.Id, []),
                        examByOffering.GetValueOrDefault(offering.Id, []),
                        asnSubmissions, examSubmissions, now);

                    // Update counters
                    switch (cStatus)
                    {
                        case "passed":
                            semPassed++;  totalPassed++;
                            semEarned        += subject.CreditHours;
                            totalEarnedCredits += subject.CreditHours;
                            semGpaPoints     += grade!.GradePoints * subject.CreditHours;
                            semCreditForGpa  += subject.CreditHours;
                            break;
                        case "failed":    semFailed++;   totalFailed++;    break;
                        case "in_progress": semInProg++; totalInProgress++; break;
                        case "withdrawn": semWithdrawn++; break;
                    }

                    courses.Add(new SubjectStatusDto
                    {
                        SubjectId   = subject.Id.ToString(),
                        SubjectName = subject.Name,
                        SubjectCode = subject.Code,
                        CreditHours = subject.CreditHours,
                        IsRequired  = isRequired,
                        Status      = cStatus,
                        GradeLetter = grade?.GradeLetter,
                        GradePoints = grade?.GradePoints,
                        FinalScore  = grade?.FinalScore,
                        IsRetake    = isRetake,
                        RetakeCount = retakeCount,
                        Activities  = activities,
                    });
                }

                // Per-semester GPA
                double? semGpa = semCreditForGpa > 0
                    ? Math.Round(semGpaPoints / semCreditForGpa, 2) : null;

                // Cumulative GPA after this semester
                cumulativeGpaPoints += semGpaPoints;
                cumulativeCreditHrs += semCreditForGpa;
                double? cumulativeGpa = cumulativeCreditHrs > 0
                    ? Math.Round(cumulativeGpaPoints / cumulativeCreditHrs, 2) : null;

                semesterDtos.Add(new SemesterRoadmapDto
                {
                    SemesterId       = sem.Id.ToString(),
                    SemesterName     = sem.Name,
                    AcademicYearName = acadYear.Name,
                    StartDate        = sem.StartDate,
                    EndDate          = sem.EndDate,
                    SemesterNumber   = semNumber,
                    Status           = semStatus,
                    SemesterGpa      = semGpa,
                    CumulativeGpaAfter = cumulativeGpa,
                    TotalSubjects    = courses.Count,
                    PassedSubjects   = semPassed,
                    FailedSubjects   = semFailed,
                    EnrolledSubjects = semInProg,
                    WithdrawnSubjects = semWithdrawn,
                    TotalCreditHours  = courses.Sum(c => c.CreditHours),
                    EarnedCreditHours = semEarned,
                    Subjects         = courses,
                });
            }

            // 11. Overall GPA (from final cumulative)
            double? currentGpa = cumulativeCreditHrs > 0
                ? Math.Round(cumulativeGpaPoints / cumulativeCreditHrs, 2) : null;

            // 12. Upcoming from regulation (subjects never enrolled)
            var everEnrolledSubjectIds = enrollments
                .Select(e => e.SubjectOffering.SubjectId).ToHashSet();

            var recommendedNext = regSubjects
                .Where(rs => !everEnrolledSubjectIds.Contains(rs.SubjectId))
                .OrderBy(rs => rs.Semester)
                .Select(rs => new SubjectStatusDto
                {
                    SubjectId   = rs.SubjectId.ToString(),
                    SubjectName = rs.Subject!.Name,
                    SubjectCode = rs.Subject.Code,
                    CreditHours = rs.Subject.CreditHours,
                    IsRequired  = rs.IsRequired,
                    Status      = "upcoming",
                }).ToList();

            // 13. Must-retake: failed + not yet passed + no active enrollment
            var passedSubjectIds = enrollments
                .Where(e => gradesByOffering.TryGetValue(e.SubjectOfferingId, out var g) && g.GradePoints >= 1.0)
                .Select(e => e.SubjectOffering.SubjectId).ToHashSet();

            var activeEnrolledSubjectIds = enrollments
                .Where(e => e.DeletedAt == null && !gradesByOffering.ContainsKey(e.SubjectOfferingId))
                .Select(e => e.SubjectOffering.SubjectId).ToHashSet();

            var mustRetake = enrollments
                .Where(e => gradesByOffering.TryGetValue(e.SubjectOfferingId, out var g) && g.GradePoints < 1.0
                         && !passedSubjectIds.Contains(e.SubjectOffering.SubjectId)
                         && !activeEnrolledSubjectIds.Contains(e.SubjectOffering.SubjectId))
                .GroupBy(e => e.SubjectOffering.SubjectId)
                .Select(g =>
                {
                    var last    = g.OrderByDescending(e => e.SubjectOffering.Semester.StartDate).First();
                    var subject = last.SubjectOffering.Subject;
                    var grade   = gradesByOffering[last.SubjectOfferingId];
                    bool isReq  = regSubjects.Any(rs => rs.SubjectId == subject.Id && rs.IsRequired);
                    return new SubjectStatusDto
                    {
                        SubjectId   = subject.Id.ToString(),
                        SubjectName = subject.Name,
                        SubjectCode = subject.Code,
                        CreditHours = subject.CreditHours,
                        IsRequired  = isReq,
                        Status      = "failed",
                        GradeLetter = grade.GradeLetter,
                        GradePoints = grade.GradePoints,
                        FinalScore  = grade.FinalScore,
                        RetakeCount = g.Count() - 1,
                    };
                }).ToList();

            // 14. Recommendations + warnings
            var recommendations = new List<string>();
            var warnings        = new List<string>();
            BuildRecommendations(currentGpa, mustRetake, totalEarnedCredits,
                                 totalRegCreditHours, totalInProgress,
                                 recommendations, warnings);

            double gradProgress = totalRegCreditHours > 0
                ? Math.Round((double)totalEarnedCredits / totalRegCreditHours * 100, 1) : 0;

            return Ok(new AcademicRoadmapDto
            {
                StudentId            = student.Id.ToString(),
                StudentName          = student.FullName,
                StudentCode          = student.Code,
                RegulationId         = regulation?.Id.ToString(),
                RegulationTitle      = regulation?.Title,
                DepartmentName       = student.Department?.Name ?? "",
                CollegeName          = student.Department?.College?.Name ?? "",
                BatchName            = student.Batch?.Name ?? "",
                TotalSemesters       = semesterDtos.Count,
                TotalCreditHours     = totalRegCreditHours,
                CompletedCreditHours = totalEarnedCredits,
                RemainingCreditHours = totalRegCreditHours - totalEarnedCredits,
                TotalSubjects        = totalRegSubjects,
                PassedSubjects       = totalPassed,
                FailedSubjects       = totalFailed,
                CurrentlyEnrolled    = totalInProgress,
                CurrentGpa           = currentGpa,
                GraduationProgressPercent = gradProgress,
                Semesters            = semesterDtos,
                RecommendedNext      = recommendedNext,
                MustRetake           = mustRetake,
                Recommendations      = recommendations,
                AcademicWarnings     = warnings,
            });
        }

        // ── Roadmap helpers ───────────────────────────────────────────────────

        private static List<RoadmapActivityDto> BuildActivities(
            List<Assignment> assignments, List<Exam> exams,
            Dictionary<Ulid, AssignmentSubmission> asnSubs,
            Dictionary<Ulid, ExamSubmission> examSubs,
            DateTime now)
        {
            var list = new List<RoadmapActivityDto>();

            foreach (var a in assignments)
            {
                asnSubs.TryGetValue(a.Id, out var sub);
                string status = sub?.Status == SubmissionStatus.Graded ? "graded"
                              : sub != null                             ? "submitted"
                              : a.Deadline < now                       ? "overdue"
                              : "pending";

                list.Add(new RoadmapActivityDto
                {
                    Id          = a.Id.ToString(),
                    Type        = "assignment",
                    Title       = a.Title,
                    DueDate     = a.Deadline,
                    SubmittedAt = sub?.SubmittedAt,
                    Status      = status,
                    Score       = sub?.Grade,
                    MaxScore    = a.MaxGrade,
                });
            }

            foreach (var e in exams)
            {
                examSubs.TryGetValue(e.Id, out var sub);
                string status = sub?.IsGraded == true    ? "graded"
                              : sub?.IsCompleted == true ? "submitted"
                              : e.EndTime < now          ? (sub != null ? "submitted" : "missed")
                              : e.StartTime <= now       ? "in_progress"
                              : "pending";

                list.Add(new RoadmapActivityDto
                {
                    Id          = e.Id.ToString(),
                    Type        = e.Type.ToString().ToLowerInvariant(),
                    Title       = e.Title,
                    DueDate     = e.EndTime,
                    SubmittedAt = sub?.SubmittedAt,
                    Status      = status,
                    Score       = sub?.Score,
                    MaxScore    = e.TotalMarks,
                });
            }

            return [.. list.OrderBy(a => a.DueDate)];
        }

        private static void BuildRecommendations(
            double? gpa, List<SubjectStatusDto> mustRetake,
            int earnedCredits, int requiredCredits, int inProgress,
            List<string> recommendations, List<string> warnings)
        {
            if (gpa < 2.0 && gpa.HasValue)
                warnings.Add($"تحذير أكاديمي: المعدل التراكمي ({gpa:F2}) أقل من الحد الأدنى (2.0)");
            else if (gpa < 2.5 && gpa.HasValue)
                warnings.Add($"تنبيه: المعدل التراكمي ({gpa:F2}) منخفض، يُنصح بالاهتمام بتحسينه");

            var requiredFails = mustRetake.Where(s => s.IsRequired).Select(s => s.SubjectName).ToList();
            if (requiredFails.Any())
                warnings.Add($"مواد إجبارية لازم تعيدها: {string.Join("، ", requiredFails)}");

            if (requiredCredits > 0)
            {
                double pct = (double)earnedCredits / requiredCredits * 100;
                if (pct >= 75)
                    recommendations.Add($"أنت قريب من التخرج! أكملت {pct:F0}% ({earnedCredits}/{requiredCredits} ساعة)");
                else if (pct >= 50)
                    recommendations.Add($"أكملت نصف رحلتك الأكاديمية ({pct:F0}%)، استمر!");
            }

            if (mustRetake.Any())
                recommendations.Add($"يُنصح بتسجيل {mustRetake.Count} مادة معادة في أقرب فصل");

            if (inProgress == 0 && !mustRetake.Any() && earnedCredits > 0)
                recommendations.Add("لا توجد تسجيلات نشطة، يُنصح بالتسجيل في الفصل القادم");
        }

        // ── POST /api/Regulations ─────────────────────────────────────────────
        /// <summary>
        /// Create a regulation. Supports two modes:
        /// 1) Text-only:          form fields: title, content, type
        /// 2) File attachment:    form fields: title, type + file (PDF/Word/Excel/TXT)
        ///    Content and File can both be provided simultaneously.
        /// NOTE: Code is auto-generated from Title as a URL-safe slug.
        ///       e.g. "General Academic Rules" → "general-academic-rules"
        /// </summary>
        [HttpPost]
        [Authorize(Roles = "Admin,SuperAdmin")]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(52_428_800)] // 50 MB
        public async Task<ActionResult<RegulationDto>> Create([FromForm] CreateRegulationDto dto)
        {
            // Validation: need at least content OR a file
            if (string.IsNullOrWhiteSpace(dto.Content) && dto.File == null)
                return BadRequest("At least one of 'content' or a file attachment must be provided.");

            // Validate file if present
            if (dto.File != null)
            {
                if (!AllowedMimeTypes.Contains(dto.File.ContentType))
                    return BadRequest($"File type '{dto.File.ContentType}' is not allowed. Supported: PDF, DOC, DOCX, XLS, XLSX, TXT.");

                if (dto.File.Length > MaxFileSizeBytes)
                    return BadRequest("File exceeds the 50 MB size limit.");
            }

            // Resolve current user (Admin/SuperAdmin) for file upload
            Ulid? uploadedFileId = null;

            if (dto.File != null)
            {
                var uploaderId = _userContext.GetUserId();

                // Reuse existing FileService — stream upload to R2 "files/" folder
                uploadedFileId = await _fileService.UploadFileStreamAsync(
                    userId: uploaderId,
                    stream: dto.File.OpenReadStream(),
                    fileName: dto.File.FileName,
                    contentType: dto.File.ContentType,
                    fileLength: dto.File.Length);
            }

            var entity = new Regulation
            {
                Title = dto.Title,
                Content = string.IsNullOrWhiteSpace(dto.Content) ? null : dto.Content,
                Type = dto.Type,
                FileId = uploadedFileId,
                IsActive = true,
                DepartmentId = string.IsNullOrEmpty(dto.DepartmentId) ? null : Ulid.Parse(dto.DepartmentId)
            };

            var subjects = new List<RegulationSubject>();
            if (!string.IsNullOrWhiteSpace(dto.SubjectsJson))
            {
                try
                {
                    var parsedSubjects = JsonSerializer.Deserialize<List<RegulationSubjectDto>>(dto.SubjectsJson, _jsonOptions);
                    if (parsedSubjects != null)
                    {
                        foreach (var s in parsedSubjects)
                        {
                            subjects.Add(new RegulationSubject
                            {
                                SubjectId = s.SubjectId,
                                Semester = s.Semester,
                                IsRequired = s.IsRequired
                            });
                        }
                    }
                }
                catch (JsonException ex)
                {
                    return BadRequest($"Invalid JSON format in SubjectsJson: {ex.Message}. Ensure you are sending a valid JSON array.");
                }
            }


            var result = await _service.CreateWithSubjectsAsync(entity, subjects);
            try { await _cache.RemoveAsync(CacheKey); } catch { }

            return Ok(await ToDto(result));
        }

        // ── PUT /api/Regulations/{id} ─────────────────────────────────────────
        [HttpPut("{id}")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(52_428_800)]
        public async Task<IActionResult> Update(string id, [FromForm] UpdateRegulationDto dto)
        {
            if (!Ulid.TryParse(id, out var regId))
                return BadRequest("Invalid Regulation ID.");

            return await UpdateRegulationCore(regId, dto);
        }

        // ── PUT /api/Regulations/by-code/{code}  [PREFERRED — admin uses code] ─
        /// <summary>
        /// [PREFERRED] Update a regulation by its auto-generated slug code.
        /// Admin/frontend MUST use this route.
        /// </summary>
        [HttpPut("by-code/{code}")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(52_428_800)]
        public async Task<IActionResult> UpdateByCode(string code, [FromForm] UpdateRegulationDto dto)
        {
            var regulation = await _service.GetByCodeAsync(code);
            if (regulation == null)
                return NotFound($"Regulation with code '{code}' not found.");

            return await UpdateRegulationCore(regulation.Id, dto);
        }

        // ── DELETE /api/Regulations/{id} ──────────────────────────────────────
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<IActionResult> Delete(string id)
        {
            if (!Ulid.TryParse(id, out var regId))
                return BadRequest("Invalid Regulation ID.");

            await _service.DeleteAsync(regId);
            try { await _cache.RemoveAsync(CacheKey); } catch { }
            return NoContent();
        }

        // ── DELETE /api/Regulations/by-code/{code}  [PREFERRED] ──────────────
        /// <summary>
        /// [PREFERRED] Delete a regulation by its auto-generated slug code.
        /// Admin/frontend MUST use this route.
        /// </summary>
        [HttpDelete("by-code/{code}")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<IActionResult> DeleteByCode(string code)
        {
            var regulation = await _service.GetByCodeAsync(code);
            if (regulation == null)
                return NotFound($"Regulation with code '{code}' not found.");

            await _service.DeleteAsync(regulation.Id);
            try { await _cache.RemoveAsync(CacheKey); } catch { }
            return NoContent();
        }

        // ── Shared update logic ───────────────────────────────────────────────
        private async Task<IActionResult> UpdateRegulationCore(Ulid regId, UpdateRegulationDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Content) && dto.File == null)
                return BadRequest("At least one of 'content' or a file attachment must be provided.");

            Ulid? uploadedFileId = null;

            if (dto.File != null)
            {
                if (dto.File.Length > MaxFileSizeBytes)
                    return BadRequest("File exceeds the 50 MB size limit.");

                if (!AllowedMimeTypes.Contains(dto.File.ContentType))
                    return BadRequest($"File type '{dto.File.ContentType}' is not allowed.");

                var uploaderId = _userContext.GetUserId();
                uploadedFileId = await _fileService.UploadFileStreamAsync(
                    userId: uploaderId,
                    stream: dto.File.OpenReadStream(),
                    fileName: dto.File.FileName,
                    contentType: dto.File.ContentType,
                    fileLength: dto.File.Length);
            }

            var entity = new Regulation
            {
                Title    = dto.Title,
                Content  = string.IsNullOrWhiteSpace(dto.Content) ? null : dto.Content,
                Type     = dto.Type,
                FileId   = uploadedFileId,
                IsActive = dto.IsActive,
                DepartmentId = string.IsNullOrEmpty(dto.DepartmentId) ? null : Ulid.Parse(dto.DepartmentId)
            };

            List<RegulationSubject>? subjects = null;
            if (dto.SubjectsJson != null)
            {
                if (string.IsNullOrWhiteSpace(dto.SubjectsJson))
                {
                    subjects = new List<RegulationSubject>(); // explicit empty
                }
                else
                {
                    try
                    {
                        subjects = new List<RegulationSubject>();
                        var parsedSubjects = JsonSerializer.Deserialize<List<RegulationSubjectDto>>(dto.SubjectsJson, _jsonOptions);
                        if (parsedSubjects != null)
                        {
                            foreach (var s in parsedSubjects)
                            {
                                subjects.Add(new RegulationSubject
                                {
                                    SubjectId = s.SubjectId,
                                    Semester = s.Semester,
                                    IsRequired = s.IsRequired
                                });
                            }
                        }
                    }
                    catch (JsonException ex)
                    {
                        return BadRequest($"Invalid JSON format in SubjectsJson: {ex.Message}. Ensure you are sending a valid JSON array.");
                    }
                }
            }

            await _service.UpdateWithSubjectsAsync(regId, entity, subjects);
            try { await _cache.RemoveAsync(CacheKey); } catch { }
            return NoContent();
        }
    }
}
