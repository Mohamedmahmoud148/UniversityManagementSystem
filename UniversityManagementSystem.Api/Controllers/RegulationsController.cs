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
        /// Returns the authenticated student's full personalized academic roadmap.
        /// Combines their regulation (لائحة) with real grade and enrollment data.
        ///
        /// Enables the AI to answer in one call:
        ///   - "ايه مواد الترم التاني في لائحتي؟"
        ///   - "كام ساعة خلصت؟"
        ///   - "المواد اللي رسبت فيها؟"
        ///   - "ايه المواد المقترحة الترم الجاي؟"
        /// </summary>
        [HttpGet("my-roadmap")]
        [Authorize(Roles = "Student,SuperAdmin")]
        public async Task<IActionResult> GetMyRoadmap()
        {
            var profileIdClaim = User.FindFirst("ProfileId");
            if (profileIdClaim == null) return Unauthorized("Student profile not found.");
            if (!Ulid.TryParse(profileIdClaim.Value, out var studentId))
                return Unauthorized("Invalid student ID.");

            // 1. Load student with navigations
            var student = await _context.Students
                .AsNoTracking()
                .Include(s => s.Department).ThenInclude(d => d.College)
                .Include(s => s.Batch)
                .FirstOrDefaultAsync(s => s.Id == studentId && s.DeletedAt == null);

            if (student == null) return NotFound("Student not found.");
            if (student.RegulationId == null)
                return NotFound("No academic regulation is assigned to your profile yet. Please contact the admin.");

            // 2. Load regulation with all subjects + subject details
            var regulation = await _context.Regulations
                .AsNoTracking()
                .Include(r => r.RegulationSubjects)
                    .ThenInclude(rs => rs.Subject)
                .FirstOrDefaultAsync(r => r.Id == student.RegulationId.Value);

            if (regulation == null) return NotFound("Assigned regulation not found.");

            // 3 + 4. Load grades and active enrollments concurrently — both depend only on studentId
            var gradesTask = _context.StudentGrades
                .AsNoTracking()
                .Where(g => g.StudentId == studentId && g.IsFinalized)
                .Select(g => new
                {
                    g.SubjectOfferingId,
                    SubjectId = g.SubjectOffering.SubjectId,
                    g.GradeLetter,
                    g.GradePoints,
                    g.FinalScore
                })
                .ToListAsync();

            var enrolledSubjectIdsTask = _context.Enrollments
                .AsNoTracking()
                .Where(e => e.StudentId == studentId && e.IsActive && e.DeletedAt == null)
                .Select(e => e.SubjectOffering.SubjectId)
                .Distinct()
                .ToListAsync();

            await Task.WhenAll(gradesTask, enrolledSubjectIdsTask);
            var grades = gradesTask.Result;
            var enrolledSubjectIds = enrolledSubjectIdsTask.Result;

            // 5. Compute GPA in-memory from already-loaded grades — no extra DB round-trip
            var positiveGradePoints = grades.Where(g => g.GradePoints > 0).Select(g => g.GradePoints).ToList();
            double? gpa = positiveGradePoints.Count > 0
                ? Math.Round(positiveGradePoints.Average(), 2)
                : null;

            // 6. Build lookup maps
            // passedSubjectIds: subject IDs where student got a passing grade (GradePoints >= 1.0 = D)
            var passedSubjectIds = grades
                .Where(g => g.GradePoints >= 1.0)
                .Select(g => g.SubjectId)
                .ToHashSet();

            var failedSubjectIds = grades
                .Where(g => g.GradePoints < 1.0)
                .Select(g => g.SubjectId)
                .ToHashSet();

            var gradeBySubject = grades
                .GroupBy(g => g.SubjectId)
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderByDescending(x => x.GradePoints).First()
                );

            var enrolledSet = enrolledSubjectIds.ToHashSet();

            // 7. Group regulation subjects by semester
            var bySemester = regulation.RegulationSubjects
                .GroupBy(rs => rs.Semester)
                .OrderBy(g => g.Key)
                .ToList();

            int totalSemesters        = bySemester.Any() ? bySemester.Max(g => g.Key) : 0;
            int totalCreditHours      = regulation.RegulationSubjects.Sum(rs => rs.Subject?.CreditHours ?? 0);
            int completedCreditHours  = 0;
            int totalPassedSubjects   = 0;
            int totalFailedSubjects   = 0;
            int totalEnrolled         = 0;

            var semesterDtos = new List<SemesterRoadmapDto>();

            foreach (var semGroup in bySemester)
            {
                var subjectDtos = semGroup.Select(rs =>
                {
                    var subj = rs.Subject;
                    if (subj == null) return null;

                    string status;
                    string? gradeLetter = null;
                    double? gradePoints = null;
                    double? finalScore  = null;

                    if (passedSubjectIds.Contains(subj.Id))
                    {
                        status = "passed";
                        if (gradeBySubject.TryGetValue(subj.Id, out var g))
                        {
                            gradeLetter = g.GradeLetter;
                            gradePoints = g.GradePoints;
                            finalScore  = g.FinalScore;
                        }
                    }
                    else if (failedSubjectIds.Contains(subj.Id) && !enrolledSet.Contains(subj.Id))
                    {
                        status = "failed";
                        if (gradeBySubject.TryGetValue(subj.Id, out var g))
                        {
                            gradeLetter = g.GradeLetter;
                            gradePoints = g.GradePoints;
                            finalScore  = g.FinalScore;
                        }
                    }
                    else if (enrolledSet.Contains(subj.Id))
                    {
                        status = "enrolled";
                    }
                    else
                    {
                        status = "upcoming";
                    }

                    return new SubjectStatusDto
                    {
                        SubjectId   = subj.Id.ToString(),
                        SubjectName = subj.Name,
                        SubjectCode = subj.Code,
                        CreditHours = subj.CreditHours,
                        IsRequired  = rs.IsRequired,
                        Status      = status,
                        GradeLetter = gradeLetter,
                        GradePoints = gradePoints,
                        FinalScore  = finalScore,
                    };
                })
                .Where(x => x != null)
                .Cast<SubjectStatusDto>()
                .ToList();

                int semPassed   = subjectDtos.Count(s => s.Status == "passed");
                int semFailed   = subjectDtos.Count(s => s.Status == "failed");
                int semEnrolled = subjectDtos.Count(s => s.Status == "enrolled");
                int semCredits  = subjectDtos.Sum(s => s.CreditHours);
                int semEarned   = subjectDtos.Where(s => s.Status == "passed").Sum(s => s.CreditHours);

                string semStatus = semPassed == subjectDtos.Count
                    ? "completed"
                    : semEnrolled > 0 || semFailed > 0
                        ? "in_progress"
                        : "upcoming";

                completedCreditHours += semEarned;
                totalPassedSubjects  += semPassed;
                totalFailedSubjects  += semFailed;
                totalEnrolled        += semEnrolled;

                semesterDtos.Add(new SemesterRoadmapDto
                {
                    SemesterNumber    = semGroup.Key,
                    Status            = semStatus,
                    TotalSubjects     = subjectDtos.Count,
                    PassedSubjects    = semPassed,
                    FailedSubjects    = semFailed,
                    EnrolledSubjects  = semEnrolled,
                    TotalCreditHours  = semCredits,
                    EarnedCreditHours = semEarned,
                    Subjects          = subjectDtos,
                });
            }

            // 8. Determine current semester (first non-completed semester)
            var currentSem = semesterDtos.FirstOrDefault(s => s.Status != "completed");
            int currentSemNumber = currentSem?.SemesterNumber ?? totalSemesters;

            // 9. Recommended next: next semester's upcoming subjects
            var nextSemNumber = currentSemNumber + 1;
            var nextSem = semesterDtos.FirstOrDefault(s => s.SemesterNumber == nextSemNumber);
            var recommendedNext = nextSem?.Subjects
                .Where(s => s.Status == "upcoming")
                .ToList() ?? [];

            // 10. Must-retake: failed required subjects not currently enrolled
            var mustRetake = semesterDtos
                .SelectMany(s => s.Subjects)
                .Where(s => s.Status == "failed" && s.IsRequired)
                .ToList();

            return Ok(new AcademicRoadmapDto
            {
                RegulationId         = regulation.Id.ToString(),
                RegulationTitle      = regulation.Title,
                DepartmentName       = student.Department?.Name ?? "",
                CollegeName          = student.Department?.College?.Name ?? "",
                BatchName            = student.Batch?.Name ?? "",
                TotalSemesters       = totalSemesters,
                TotalCreditHours     = totalCreditHours,
                CompletedCreditHours = completedCreditHours,
                RemainingCreditHours = totalCreditHours - completedCreditHours,
                TotalSubjects        = regulation.RegulationSubjects.Count,
                PassedSubjects       = totalPassedSubjects,
                FailedSubjects       = totalFailedSubjects,
                CurrentlyEnrolled    = totalEnrolled,
                CurrentGpa           = gpa,
                Semesters            = semesterDtos,
                RecommendedNext      = recommendedNext,
                MustRetake           = mustRetake,
            });
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
