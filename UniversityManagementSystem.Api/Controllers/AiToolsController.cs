using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NUlid;
using UniversityManagementSystem.Core.DTOs;
using UniversityManagementSystem.Core.Entities;
using UniversityManagementSystem.Core.Interfaces;
using UniversityManagementSystem.Infrastructure.Data;

namespace UniversityManagementSystem.Api.Controllers
{
    // ─────────────────────────────────────────────
    // DTOs
    // ─────────────────────────────────────────────

    // ── AI tool DTOs ──────────────────────────────────────────────────────
    public record ResolveStudentResult(string StudentId, string StudentName, string StudentCode);
    public record ResolveDoctorResult(string DoctorId, string DoctorName, string DoctorCode);
    public record StudentGpaResult(string StudentId, double Gpa);
    /// <summary>Includes studentCode so AI agents have the full id+code+name triple.</summary>
    public record OfferingStudentItem(string StudentId, string StudentName, string StudentCode);
    public record DoctorSubjectItem(string SubjectId, string SubjectName, string SubjectCode);
    /// <summary>A single matching offering returned by resolve-offering — list allows AI to discriminate by semester/batch.</summary>
    public record ResolveOfferingItem(
        string OfferingId,
        string SubjectId,
        string SubjectName,
        string SubjectCode,
        string SemesterId,
        string SemesterName,
        string? BatchId,
        string? GroupId);

    // ─────────────────────────────────────────────
    // Controller
    // ─────────────────────────────────────────────

    [ApiController]
    [Route("api/ai-tools")]
    [Authorize]   // All endpoints require authentication
    public class AiToolsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<AiToolsController> _logger;
        private readonly IGradeService _gradeService;
        private readonly IExcelImportService _excelImport;
        private readonly ISystemUserResolver _userResolver;

        public AiToolsController(
            AppDbContext context,
            ILogger<AiToolsController> logger,
            IGradeService gradeService,
            IExcelImportService excelImport,
            ISystemUserResolver userResolver)
        {
            _context = context;
            _logger = logger;
            _gradeService = gradeService;
            _excelImport = excelImport;
            _userResolver = userResolver;
        }

        // ── 1. Resolve Subject by Name ──────────────────────────────────────
        /// <summary>GET /api/ai-tools/resolve-subject?name={subjectName}</summary>
        [HttpGet("resolve-subject")]
        [Authorize(Roles = "Admin,SuperAdmin,Doctor")]
        public async Task<IActionResult> ResolveSubject([FromQuery] string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return BadRequest("Subject name is required.");

            var lower = name.ToLower();

            var result = await _context.Subjects
                .Where(s => s.Name.ToLower() == lower)
                .Select(s => new
                {
                    subjectId   = s.Id.ToString(),
                    subjectName = s.Name,
                    subjectCode = s.Code
                })
                .FirstOrDefaultAsync();

            if (result is null)
            {
                _logger.LogWarning("AI Tool: subject not found by name '{Name}'", name);
                return NotFound($"No subject found with name '{name}'.");
            }

            return Ok(result);
        }

        // ── 2. Resolve Subject Offering — returns a LIST (AI picks the right one) ───
        /// <summary>
        /// GET /api/ai-tools/resolve-offering?subject={subjectName}
        /// Returns ALL active offerings for the subject name.
        /// The AI must select the correct one using semester/batch/group context.
        /// NEVER uses FirstOrDefault — temporal collision risk eliminated.
        /// </summary>
        [HttpGet("resolve-offering")]
        [Authorize(Roles = "Admin,SuperAdmin,Doctor")]
        public async Task<IActionResult> ResolveOffering([FromQuery] string subject)
        {
            if (string.IsNullOrWhiteSpace(subject))
                return BadRequest("Subject name is required.");

            var lower = subject.ToLower();

            var results = await _context.SubjectOfferings
                .Include(o => o.Subject)
                .Include(o => o.Semester)
                .Where(o => o.Subject.Name.ToLower() == lower)
                .OrderByDescending(o => o.CreatedAt)
                .Select(o => new ResolveOfferingItem(
                    o.Id.ToString(),
                    o.SubjectId.ToString(),
                    o.Subject.Name,
                    o.Subject.Code,
                    o.SemesterId.ToString(),
                    o.Semester.Name,
                    o.BatchId != default ? o.BatchId.ToString() : null,
                    o.GroupId.HasValue ? o.GroupId.Value.ToString() : null))
                .ToListAsync();

            if (results.Count == 0)
            {
                _logger.LogWarning("AI Tool: no offering found for subject '{Subject}'", subject);
                return NotFound($"No offerings found for subject '{subject}'.");
            }

            return Ok(new
            {
                count     = results.Count,
                note      = "Multiple offerings found. Select using semesterName and/or batchId.",
                offerings = results
            });
        }

        // ── 3. Student Academic Overview ────────────────────────────────────
        /// <summary>GET /api/ai-tools/student-overview/{studentId}</summary>
        [HttpGet("student-overview/{studentId}")]
        [Authorize(Roles = "Admin,SuperAdmin,Doctor,Student")]
        public async Task<IActionResult> GetStudentOverview(string studentId)
        {
            if (!Ulid.TryParse(studentId, out var parsedId))
                return BadRequest("Invalid student ID format.");

            var exists = await _context.Students.AnyAsync(s => s.Id == parsedId);
            if (!exists)
                return NotFound($"Student '{studentId}' not found.");

            // ── GPA via GradeService (unified, credit-hour-weighted) ──────────
            var gpaDto = await _gradeService.CalculateStudentGpaAsync(parsedId);

            // Active subject enrolments
            var subjects = await _context.Enrollments
                .Include(e => e.SubjectOffering)
                    .ThenInclude(so => so.Subject)
                .Where(e => e.StudentId == parsedId && e.IsActive)
                .Select(e => new
                {
                    subjectId   = e.SubjectOffering.SubjectId.ToString(),
                    subjectName = e.SubjectOffering.Subject.Name,
                    subjectCode = e.SubjectOffering.Subject.Code,
                    offeringId  = e.SubjectOfferingId.ToString()
                })
                .ToListAsync();

            // Finalised grades
            var grades = await _context.StudentGrades
                .Include(g => g.SubjectOffering)
                    .ThenInclude(so => so.Subject)
                .Where(g => g.StudentId == parsedId && g.IsFinalized)
                .Select(g => new
                {
                    subjectId   = g.SubjectOffering.SubjectId.ToString(),
                    subjectName = g.SubjectOffering.Subject.Name,
                    subjectCode = g.SubjectOffering.Subject.Code,
                    gradeLetter = g.GradeLetter,
                    finalScore  = g.FinalScore
                })
                .ToListAsync();

            // Exam submissions
            var exams = await _context.ExamSubmissions
                .Include(es => es.Exam)
                    .ThenInclude(e => e.SubjectOffering)
                        .ThenInclude(so => so.Subject)
                .Where(es => es.StudentId == parsedId)
                .Select(es => new
                {
                    examTitle   = es.Exam.Title,
                    subjectName = es.Exam.SubjectOffering.Subject.Name,
                    score       = es.Score,
                    totalMarks  = es.Exam.TotalMarks,
                    isGraded    = es.IsGraded
                })
                .ToListAsync();

            return Ok(new
            {
                studentId  = studentId,
                gpa        = gpaDto.GPA,
                totalCreditHours = gpaDto.TotalCreditHours,
                subjects,
                grades,
                exams
            });
        }

        // ── 5. Resolve Student by Name ──────────────────────────────────────
        /// <summary>GET /api/ai-tools/resolve-student?name={name}</summary>
        [HttpGet("resolve-student")]
        [Authorize(Roles = "Admin,SuperAdmin,Doctor")]
        public async Task<IActionResult> ResolveStudent([FromQuery] string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return BadRequest("Student name is required.");

            var pattern = $"%{name}%";

            var result = await _context.Students
                .AsNoTracking()
                .Where(s => EF.Functions.ILike(s.FullName, pattern))
                .OrderBy(s => s.FullName)
                .Select(s => new ResolveStudentResult(
                    s.Id.ToString(),
                    s.FullName,
                    s.Code))
                .FirstOrDefaultAsync();

            if (result is null)
            {
                _logger.LogWarning("AI Tool: student not found by name '{Name}'", name);
                return NotFound($"No student found matching '{name}'.");
            }

            return Ok(result);
        }

        // ── 6. Resolve Doctor by Name ───────────────────────────────────────
        /// <summary>GET /api/ai-tools/resolve-doctor?name={name}</summary>
        [HttpGet("resolve-doctor")]
        [Authorize(Roles = "Admin,SuperAdmin,Doctor")]
        public async Task<IActionResult> ResolveDoctor([FromQuery] string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return BadRequest("Doctor name is required.");

            var pattern = $"%{name}%";

            var result = await _context.Doctors
                .AsNoTracking()
                .Where(d => EF.Functions.ILike(d.FullName, pattern))
                .OrderBy(d => d.FullName)
                .Select(d => new ResolveDoctorResult(
                    d.Id.ToString(),
                    d.FullName,
                    d.Code))
                .FirstOrDefaultAsync();

            if (result is null)
            {
                _logger.LogWarning("AI Tool: doctor not found by name '{Name}'", name);
                return NotFound($"No doctor found matching '{name}'.");
            }

            return Ok(result);
        }

        // ── 7. Student GPA (unified via GradeService) ───────────────────────
        /// <summary>GET /api/ai-tools/student-gpa/{studentId}</summary>
        [HttpGet("student-gpa/{studentId}")]
        [Authorize(Roles = "Admin,SuperAdmin,Doctor,Student")]
        public async Task<IActionResult> GetStudentGpa(string studentId)
        {
            if (!Ulid.TryParse(studentId, out var parsedId))
                return BadRequest("Invalid student ID format.");

            var exists = await _context.Students.AnyAsync(s => s.Id == parsedId);
            if (!exists)
                return NotFound($"Student '{studentId}' not found.");

            // Delegate to GradeService — credit-hour-weighted, authoritative calculation
            var gpaDto = await _gradeService.CalculateStudentGpaAsync(parsedId);

            return Ok(new StudentGpaResult(studentId, gpaDto.GPA));
        }

        // ── 8. Offering Students ────────────────────────────────────────────
        /// <summary>GET /api/ai-tools/offering-students/{offeringId}</summary>
        [HttpGet("offering-students/{offeringId}")]
        [Authorize(Roles = "Admin,SuperAdmin,Doctor")]
        public async Task<IActionResult> GetOfferingStudents(string offeringId)
        {
            if (!Ulid.TryParse(offeringId, out var parsedId))
                return BadRequest("Invalid offering ID format.");

            var offeringExists = await _context.SubjectOfferings
                .AnyAsync(o => o.Id == parsedId);

            if (!offeringExists)
                return NotFound($"Offering '{offeringId}' not found.");

            var students = await _context.Enrollments
                .AsNoTracking()
                .Include(e => e.Student)
                .Where(e => e.SubjectOfferingId == parsedId && e.IsActive)
                .Select(e => new OfferingStudentItem(
                    e.StudentId.ToString(),
                    e.Student.FullName,
                    e.Student.Code))
                .OrderBy(x => x.StudentName)
                .ToListAsync();

            return Ok(students);
        }

        // ── 9. Doctor Subjects ──────────────────────────────────────────────
        /// <summary>GET /api/ai-tools/doctor-subjects/{doctorId}</summary>
        [HttpGet("doctor-subjects/{doctorId}")]
        [Authorize(Roles = "Admin,SuperAdmin,Doctor,Student")]
        public async Task<IActionResult> GetDoctorSubjects(string doctorId)
        {
            if (!Ulid.TryParse(doctorId, out var parsedId))
                return BadRequest("Invalid doctor ID format.");

            var doctorExists = await _context.Doctors.AnyAsync(d => d.Id == parsedId);
            if (!doctorExists)
                return NotFound($"Doctor '{doctorId}' not found.");

            var subjects = await _context.SubjectOfferings
                .AsNoTracking()
                .Include(o => o.Subject)
                .Where(o => o.DoctorId == parsedId)
                .Select(o => new DoctorSubjectItem(
                    o.SubjectId.ToString(),
                    o.Subject.Name,
                    o.Subject.Code))
                .Distinct()
                .OrderBy(x => x.SubjectName)
                .ToListAsync();

            return Ok(subjects);
        }

        // ── Complaint System ────────────────────────────────────────────────

        /// <summary>
        /// POST /api/ai-tools/create-complaint
        /// Allows a student to submit a complaint.
        /// The doctor linked to the specified offering is resolved automatically
        /// and stored in TargetDoctorId for efficient filtering.
        /// </summary>
        [HttpPost("create-complaint")]
        [Authorize(Roles = "Student,SuperAdmin")]
        public async Task<IActionResult> CreateComplaint([FromBody] CreateComplaintDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            // Validate TargetType
            var allowedTypes = new[] { "Doctor", "Exam", "Grade", "Other" };
            if (!allowedTypes.Contains(dto.TargetType, StringComparer.OrdinalIgnoreCase))
                return BadRequest(new { error = $"TargetType must be one of: {string.Join(", ", allowedTypes)}." });

            var userId = await _userResolver.ResolveSystemUserIdAsync(User);

            // Basic entity logic since the target validation is handled by AI

            var complaint = new Complaint
            {
                StudentId         = userId,
                Title             = dto.Title,
                TargetType        = dto.TargetType,
                TargetId          = dto.TargetId ?? string.Empty,
                Message           = dto.Message,
                Status            = "Pending",
                Priority          = "Normal",
                CreatedAt         = DateTime.UtcNow
            };

            _context.Complaints.Add(complaint);
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "[Complaint] Created — id={Id} userId={UserId} targetType={TargetType}",
                complaint.Id, userId, dto.TargetType);

            return CreatedAtAction(nameof(GetComplaints), null, new
            {
                id          = complaint.Id.ToString(),
                status      = complaint.Status,
                createdAt   = complaint.CreatedAt
            });
        }

        /// <summary>
        /// GET /api/ai-tools/get-complaints
        /// Returns a paginated, filtered list of complaints.
        /// Admin: sees all. Doctor: sees only complaints in their offerings.
        /// </summary>
        [HttpGet("get-complaints")]
        [Authorize(Roles = "Admin,SuperAdmin,Doctor")]
        public async Task<IActionResult> GetComplaints([FromQuery] GetComplaintsQueryDto query)
        {
            query.PageSize = Math.Clamp(query.PageSize, 1, 100);
            query.Page     = Math.Max(query.Page, 1);

            var q = _context.Complaints.AsNoTracking().AsQueryable();

            // Date range filter — hits IX_Complaints_CreatedAt
            if (query.From.HasValue) q = q.Where(c => c.CreatedAt >= query.From.Value);
            if (query.To.HasValue)   q = q.Where(c => c.CreatedAt <= query.To.Value);

            // Target filter
            if (!string.IsNullOrWhiteSpace(query.TargetType))
                q = q.Where(c => c.TargetType == query.TargetType);

            if (!string.IsNullOrWhiteSpace(query.TargetId))
                q = q.Where(c => c.TargetId == query.TargetId);

            // Status filter
            if (!string.IsNullOrWhiteSpace(query.Status))
                q = q.Where(c => c.Status == query.Status);

            // Doctor-specific scope: a doctor can only see complaints in their own offerings
            var role = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? "";
            if (string.Equals(role, "Doctor", StringComparison.OrdinalIgnoreCase))
            {
                var callerId = await _userResolver.ResolveSystemUserIdAsync(User);
                var doctor = await _context.Doctors
                    .AsNoTracking()
                    .FirstOrDefaultAsync(d => d.SystemUserId == callerId);

                if (doctor == null) return Forbid();

                q = q.Where(c => c.TargetId == doctor.Id.ToString() && c.TargetType == "Doctor");
            }

            var total = await q.CountAsync();

            var items = await q
                .OrderByDescending(c => c.CreatedAt)
                .Skip((query.Page - 1) * query.PageSize)
                .Take(query.PageSize)
                .Select(c => new ComplaintDto
                {
                    Id                = c.Id.ToString(),
                    StudentId         = c.StudentId.ToString(),
                    Title             = c.Title,
                    TargetType        = c.TargetType,
                    TargetId          = c.TargetId,
                    Message           = c.Message,
                    Status            = c.Status,
                    Priority          = c.Priority,
                    ResolutionNote    = c.ResolutionNote,
                    CreatedAt         = c.CreatedAt
                })
                .ToListAsync();

            return Ok(new ComplaintsPageDto
            {
                TotalCount = total,
                Page       = query.Page,
                PageSize   = query.PageSize,
                Items      = items
            });
        }

        // ── Bulk Operations ─────────────────────────────────────────────────

        /// <summary>
        /// POST /api/ai-tools/bulk-create-students
        /// Accepts an .xlsx file and bulk-imports students.
        /// Required columns: FullName | Email | UniversityStudentId | BatchCode | GroupCode
        /// </summary>
        [HttpPost("bulk-create-students")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        [RequestSizeLimit(10 * 1024 * 1024)]   // 10 MB cap
        public async Task<IActionResult> BulkCreateStudents(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { error = "No file uploaded. Please attach an .xlsx file." });

            var ext = Path.GetExtension(file.FileName);
            if (!ext.Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
                return BadRequest(new { error = $"Invalid format '{ext}'. Only .xlsx is accepted." });

            _logger.LogInformation(
                "[BulkStudents] Import started — file={File} size={Size}B",
                file.FileName, file.Length);

            var result       = await _excelImport.ImportStudentsFromExcelAsync(file);
            var importFailed = result.Errors.Count > 0 ? result.Skipped : 0;

            _logger.LogInformation(
                "[BulkStudents] Import complete — inserted={Ins} skipped={Skip}",
                result.Imported, result.Skipped);

            var status = result.Skipped == 0 ? StatusCodes.Status200OK : StatusCodes.Status207MultiStatus;

            return StatusCode(status, new BulkOperationResultDto
            {
                TotalRows = result.TotalRows,
                Inserted  = result.Imported,
                Skipped   = result.Skipped,
                Failed    = importFailed,
                Errors    = result.Errors
            });
        }

        /// <summary>
        /// POST /api/ai-tools/bulk-upload-grades
        /// Accepts an .xlsx file with columns:
        ///   UniversityStudentId | SubjectOfferingId | FinalScore | GradeLetter | GradePoints
        /// Upserts StudentGrade rows; duplicate (StudentId+OfferingId) rows are updated.
        /// </summary>
        [HttpPost("bulk-upload-grades")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        [RequestSizeLimit(10 * 1024 * 1024)]
        public async Task<IActionResult> BulkUploadGrades(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { error = "No file uploaded. Please attach an .xlsx file." });

            var ext = Path.GetExtension(file.FileName);
            if (!ext.Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
                return BadRequest(new { error = $"Invalid format '{ext}'. Only .xlsx is accepted." });

            _logger.LogInformation(
                "[BulkGrades] Import started — file={File} size={Size}B",
                file.FileName, file.Length);

            var result = new BulkOperationResultDto();

            using var stream = new System.IO.MemoryStream();
            await file.CopyToAsync(stream);
            stream.Position = 0;

            using var workbook = new ClosedXML.Excel.XLWorkbook(stream);
            var worksheet = workbook.Worksheet(1);
            var range     = worksheet.RangeUsed();

            if (range == null)
                return BadRequest(new { error = "Worksheet is empty." });

            var rows = range.RowsUsed().Skip(1).ToList();   // Skip header
            result.TotalRows = rows.Count;

            if (result.TotalRows == 0)
            {
                result.Errors.Add("No data rows found (only header present).");
                return BadRequest(result);
            }

            // Required column headers (validated positionally)
            // Col 1=UniversityStudentId | 2=SubjectOfferingId | 3=FinalScore | 4=GradeLetter | 5=GradePoints

            // Pre-fetch all student and offering ID mappings to avoid N+1
            var studentsByUnivId = await _context.Students
                .AsNoTracking()
                .ToDictionaryAsync(s => s.UniversityStudentId.ToLower(), s => s.Id);

            var validOfferingIds = await _context.SubjectOfferings
                .AsNoTracking()
                .Select(o => o.Id)
                .ToHashSetAsync();

            var existingGrades = await _context.StudentGrades
                .ToDictionaryAsync(g => (g.StudentId, g.SubjectOfferingId));

            // In-batch duplicates
            var batchPairs = new HashSet<(Ulid, Ulid)>();

            var toInsert = new List<StudentGrade>();

            foreach (var row in rows)
            {
                int rowNum = row.RowNumber();
                try
                {
                    var univStudId   = row.Cell(1).GetValue<string>().Trim();
                    var offeringStr  = row.Cell(2).GetValue<string>().Trim();
                    var finalScoreRaw = row.Cell(3).GetValue<string>().Trim();
                    var gradeLetter  = row.Cell(4).GetValue<string>().Trim();
                    var gradePointsRaw = row.Cell(5).GetValue<string>().Trim();

                    // Required fields
                    if (string.IsNullOrWhiteSpace(univStudId) || string.IsNullOrWhiteSpace(offeringStr) ||
                        string.IsNullOrWhiteSpace(finalScoreRaw) || string.IsNullOrWhiteSpace(gradeLetter))
                    {
                        result.Skipped++;
                        result.Errors.Add($"Row {rowNum}: One or more required fields are empty — skipped.");
                        continue;
                    }

                    if (!double.TryParse(finalScoreRaw, System.Globalization.NumberStyles.Any,
                            System.Globalization.CultureInfo.InvariantCulture, out double finalScore) ||
                        finalScore < 0 || finalScore > 100)
                    {
                        result.Skipped++;
                        result.Errors.Add($"Row {rowNum}: FinalScore '{finalScoreRaw}' is not a valid number (0-100) — skipped.");
                        continue;
                    }

                    double gradePoints = 0;
                    if (!string.IsNullOrWhiteSpace(gradePointsRaw))
                        double.TryParse(gradePointsRaw, System.Globalization.NumberStyles.Any,
                            System.Globalization.CultureInfo.InvariantCulture, out gradePoints);

                    if (!Ulid.TryParse(offeringStr, out var offeringId))
                    {
                        result.Skipped++;
                        result.Errors.Add($"Row {rowNum}: SubjectOfferingId '{offeringStr}' is not a valid ULID — skipped.");
                        continue;
                    }

                    if (!validOfferingIds.Contains(offeringId))
                    {
                        result.Skipped++;
                        result.Errors.Add($"Row {rowNum}: SubjectOffering '{offeringStr}' not found — skipped.");
                        continue;
                    }

                    if (!studentsByUnivId.TryGetValue(univStudId.ToLower(), out var studentId))
                    {
                        result.Skipped++;
                        result.Errors.Add($"Row {rowNum}: Student '{univStudId}' not found — skipped.");
                        continue;
                    }

                    // In-batch duplicate check
                    var pair = (studentId, offeringId);
                    if (batchPairs.Contains(pair))
                    {
                        result.Skipped++;
                        result.Errors.Add($"Row {rowNum}: Duplicate (StudentId, OfferingId) in file — skipped.");
                        continue;
                    }
                    batchPairs.Add(pair);

                    // Upsert: update existing grade or queue new insert
                    if (existingGrades.TryGetValue((studentId, offeringId), out var existing))
                    {
                        existing.FinalScore   = finalScore;
                        existing.GradeLetter  = gradeLetter;
                        existing.GradePoints  = gradePoints;
                        existing.IsFinalized  = true;
                        existing.CalculatedAt = DateTime.UtcNow;
                        _context.Entry(existing).State = EntityState.Modified;
                        result.Inserted++;    // counts as upserted
                    }
                    else
                    {
                        toInsert.Add(new StudentGrade
                        {
                            StudentId         = studentId,
                            SubjectOfferingId = offeringId,
                            FinalScore        = finalScore,
                            GradeLetter       = gradeLetter,
                            GradePoints       = gradePoints,
                            IsFinalized       = true,
                            CalculatedAt      = DateTime.UtcNow
                        });
                        result.Inserted++;
                    }
                }
                catch (Exception ex)
                {
                    result.Failed++;
                    result.Errors.Add($"Row {rowNum}: Unexpected error — {ex.Message} — skipped.");
                }
            }

            // Single batch save
            if (toInsert.Count > 0)
                _context.StudentGrades.AddRange(toInsert);

            if (toInsert.Count > 0 || _context.ChangeTracker.HasChanges())
                await _context.SaveChangesAsync();

            _logger.LogInformation(
                "[BulkGrades] Complete — inserted/updated={Ins} skipped={Skip} failed={Fail}",
                result.Inserted, result.Skipped, result.Failed);

            var httpStatus = result.Failed == 0 ? StatusCodes.Status200OK : StatusCodes.Status207MultiStatus;
            return StatusCode(httpStatus, result);
        }
    }
}
