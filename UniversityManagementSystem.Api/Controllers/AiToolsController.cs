using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NUlid;
using UniversityManagementSystem.Core.Entities;
using UniversityManagementSystem.Infrastructure.Data;

namespace UniversityManagementSystem.Api.Controllers
{
    // ─────────────────────────────────────────────
    // DTOs
    // ─────────────────────────────────────────────

    public class DistributeExamsRequest
    {
        public List<string> ExamIds { get; set; } = new();
        public string OfferingId { get; set; } = string.Empty;
    }

    // ── New AI tool DTOs ──────────────────────────────────────────────────
    public record ResolveStudentResult(string StudentId, string StudentName, string StudentCode);
    public record ResolveDoctorResult(string DoctorId, string DoctorName, string DoctorCode);
    public record StudentGpaResult(string StudentId, double Gpa);
    public record OfferingStudentItem(string StudentId, string StudentName);
    public record DoctorSubjectItem(string SubjectId, string SubjectName, string SubjectCode);

    // ─────────────────────────────────────────────
    // Controller
    // ─────────────────────────────────────────────

    [ApiController]
    [Route("api/ai-tools")]
    public class AiToolsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<AiToolsController> _logger;

        public AiToolsController(AppDbContext context, ILogger<AiToolsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // ── 1. Resolve Subject by Name ──────────────────────────────────────
        /// <summary>GET /api/ai-tools/resolve-subject?name={subjectName}</summary>
        [HttpGet("resolve-subject")]
        public async Task<IActionResult> ResolveSubject([FromQuery] string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return BadRequest("Subject name is required.");

            var lower = name.ToLower();

            var result = await _context.Subjects
                .Where(s => s.Name.ToLower() == lower)
                .Select(s => new
                {
                    subjectId = s.Id.ToString(),
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

        // ── 2. Resolve Subject Offering ─────────────────────────────────────
        /// <summary>GET /api/ai-tools/resolve-offering?subject={subjectName}</summary>
        [HttpGet("resolve-offering")]
        public async Task<IActionResult> ResolveOffering([FromQuery] string subject)
        {
            if (string.IsNullOrWhiteSpace(subject))
                return BadRequest("Subject name is required.");

            var lower = subject.ToLower();

            var result = await _context.SubjectOfferings
                .Include(o => o.Subject)
                .Include(o => o.Semester)
                .Where(o => o.Subject.Name.ToLower() == lower)
                .OrderByDescending(o => o.CreatedAt)
                .Select(o => new
                {
                    subjectOfferingId = o.Id.ToString(),
                    semester = o.Semester.Name
                })
                .FirstOrDefaultAsync();

            if (result is null)
            {
                _logger.LogWarning("AI Tool: no offering found for subject '{Subject}'", subject);
                return NotFound($"No offering found for subject '{subject}'.");
            }

            return Ok(result);
        }

        // ── 3. Student Academic Overview ────────────────────────────────────
        /// <summary>GET /api/ai-tools/student-overview/{studentId}</summary>
        [HttpGet("student-overview/{studentId}")]
        public async Task<IActionResult> GetStudentOverview(string studentId)
        {
            if (!Ulid.TryParse(studentId, out var parsedId))
                return BadRequest("Invalid student ID format.");

            var exists = await _context.Students.AnyAsync(s => s.Id == parsedId);
            if (!exists)
                return NotFound($"Student '{studentId}' not found.");

            // GPA – average of finalised grade points (null if no grades yet)
            var gpa = await _context.StudentGrades
                .Where(g => g.StudentId == parsedId && g.IsFinalized)
                .AverageAsync(g => (double?)g.GradePoints) ?? 0.0;

            // Active subject enrolments
            var subjects = await _context.Enrollments
                .Include(e => e.SubjectOffering)
                    .ThenInclude(so => so.Subject)
                .Where(e => e.StudentId == parsedId && e.IsActive)
                .Select(e => new
                {
                    subjectName = e.SubjectOffering.Subject.Name,
                    subjectCode = e.SubjectOffering.Subject.Code
                })
                .ToListAsync();

            // Finalised grades
            var grades = await _context.StudentGrades
                .Include(g => g.SubjectOffering)
                    .ThenInclude(so => so.Subject)
                .Where(g => g.StudentId == parsedId && g.IsFinalized)
                .Select(g => new
                {
                    subjectName = g.SubjectOffering.Subject.Name,
                    gradeLetter = g.GradeLetter,
                    finalScore = g.FinalScore
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
                    examTitle = es.Exam.Title,
                    subjectName = es.Exam.SubjectOffering.Subject.Name,
                    score = es.Score,
                    totalMarks = es.Exam.TotalMarks,
                    isGraded = es.IsGraded
                })
                .ToListAsync();

            return Ok(new
            {
                studentId = studentId,
                gpa = Math.Round(gpa, 2),
                subjects,
                grades,
                exams
            });
        }

        // ── 4. Distribute Exams Randomly ────────────────────────────────────
        /// <summary>POST /api/ai-tools/distribute-exams</summary>
        [HttpPost("distribute-exams")]
        public async Task<IActionResult> DistributeExams([FromBody] DistributeExamsRequest request)
        {
            if (request is null)
                return BadRequest("Request body is required.");

            if (!Ulid.TryParse(request.OfferingId, out var parsedOfferingId))
                return BadRequest("Invalid offering ID format.");

            if (request.ExamIds is null || request.ExamIds.Count == 0)
                return BadRequest("At least one exam ID is required.");

            // Parse all exam IDs up front
            var examIds = new List<Ulid>();
            foreach (var raw in request.ExamIds)
            {
                if (!Ulid.TryParse(raw, out var eid))
                    return BadRequest($"Invalid exam ID format: '{raw}'.");
                examIds.Add(eid);
            }

            var offeringExists = await _context.SubjectOfferings
                .AnyAsync(o => o.Id == parsedOfferingId);

            if (!offeringExists)
                return NotFound($"Offering '{request.OfferingId}' not found.");

            // Load enrolled students
            var studentIds = await _context.Enrollments
                .Where(e => e.SubjectOfferingId == parsedOfferingId && e.IsActive)
                .Select(e => e.StudentId)
                .ToListAsync();

            if (!studentIds.Any())
                return BadRequest("No active students are enrolled in this offering.");

            // Randomly assign one exam per student (skip if assignment already exists)
            var rng = new Random();
            var toInsert = new List<ExamSubmission>();
            var skippedCount = 0;

            foreach (var sid in studentIds)
            {
                var randomExamId = examIds[rng.Next(examIds.Count)];

                var alreadyAssigned = await _context.ExamSubmissions
                    .AnyAsync(s => s.StudentId == sid && s.ExamId == randomExamId);

                if (alreadyAssigned)
                {
                    skippedCount++;
                    continue;
                }

                toInsert.Add(new ExamSubmission
                {
                    StudentId = sid,
                    ExamId = randomExamId,
                    IsGraded = false,
                    AnswersJson = "[]"
                });
            }

            if (toInsert.Any())
            {
                await _context.ExamSubmissions.AddRangeAsync(toInsert);
                await _context.SaveChangesAsync();
            }

            return Ok(new
            {
                message = "Exams distributed successfully.",
                studentsProcessed = studentIds.Count,
                newAssignments = toInsert.Count,
                skippedDuplicates = skippedCount
            });
        }

        // ── 5. Resolve Student by Name ──────────────────────────────────────
        /// <summary>GET /api/ai-tools/resolve-student?name={name}</summary>
        [HttpGet("resolve-student")]
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

        // ── 7. Student GPA ──────────────────────────────────────────────────
        /// <summary>GET /api/ai-tools/student-gpa/{studentId}</summary>
        [HttpGet("student-gpa/{studentId}")]
        public async Task<IActionResult> GetStudentGpa(string studentId)
        {
            if (!Ulid.TryParse(studentId, out var parsedId))
                return BadRequest("Invalid student ID format.");

            var exists = await _context.Students.AnyAsync(s => s.Id == parsedId);
            if (!exists)
                return NotFound($"Student '{studentId}' not found.");

            // Average of finalized GradePoints; 0.0 when no grades are recorded yet
            var gpa = await _context.StudentGrades
                .AsNoTracking()
                .Where(g => g.StudentId == parsedId && g.IsFinalized)
                .AverageAsync(g => (double?)g.GradePoints) ?? 0.0;

            return Ok(new StudentGpaResult(studentId, Math.Round(gpa, 2)));
        }

        // ── 8. Offering Students ────────────────────────────────────────────
        /// <summary>GET /api/ai-tools/offering-students/{offeringId}</summary>
        [HttpGet("offering-students/{offeringId}")]
        public async Task<IActionResult> GetOfferingStudents(string offeringId)
        {
            if (!Ulid.TryParse(offeringId, out var parsedId))
                return BadRequest("Invalid offering ID format.");

            var offeringExists = await _context.SubjectOfferings
                .AnyAsync(o => o.Id == parsedId);

            if (!offeringExists)
                return NotFound($"Offering '{offeringId}' not found.");

            // Active enrollments only
            var students = await _context.Enrollments
                .AsNoTracking()
                .Include(e => e.Student)
                .Where(e => e.SubjectOfferingId == parsedId && e.IsActive)
                .Select(e => new OfferingStudentItem(
                    e.StudentId.ToString(),
                    e.Student.FullName))
                .OrderBy(x => x.StudentName)
                .ToListAsync();

            return Ok(students);
        }

        // ── 9. Doctor Subjects ──────────────────────────────────────────────
        /// <summary>GET /api/ai-tools/doctor-subjects/{doctorId}</summary>
        [HttpGet("doctor-subjects/{doctorId}")]
        public async Task<IActionResult> GetDoctorSubjects(string doctorId)
        {
            if (!Ulid.TryParse(doctorId, out var parsedId))
                return BadRequest("Invalid doctor ID format.");

            var doctorExists = await _context.Doctors.AnyAsync(d => d.Id == parsedId);
            if (!doctorExists)
                return NotFound($"Doctor '{doctorId}' not found.");

            // A doctor teaches subjects via SubjectOfferings where DoctorId matches.
            // Distinct subjects are returned (a doctor may teach the same subject in multiple semesters).
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
    }
}
