using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using UniversityManagementSystem.Core.DTOs;
using UniversityManagementSystem.Core.DTOs.Ai;
using UniversityManagementSystem.Core.Interfaces;
using NUlid;

namespace UniversityManagementSystem.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ExamsController(IExamService examService, IAiService aiService) : ControllerBase
    {

        [HttpGet("by-code/{code}")]
        [Authorize(Roles = "Admin, Doctor, Student,SuperAdmin")]
        public async Task<IActionResult> GetByCode(string code)
        {
            var profileClaim = User.Claims.FirstOrDefault(c => c.Type == "ProfileId")?.Value;
            var roleClaim = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role)?.Value;

            if (profileClaim == null || roleClaim == null) return Unauthorized();

            var exam = await examService.GetExamByCodeAsync(code, Ulid.Parse(profileClaim), roleClaim);
            if (exam == null) return NotFound();

            return Ok(exam);
        }

        [HttpPost]
        [Authorize(Roles = "Doctor,SuperAdmin")]
        public async Task<IActionResult> CreateExam([FromQuery] string? subjectOfferingId, [FromBody] CreateExamDto dto)
        {
            // Accept subjectOfferingId from body OR query param (body takes priority)
            var rawId = dto.SubjectOfferingId ?? subjectOfferingId ?? string.Empty;
            if (!Ulid.TryParse(rawId, out var offeringId))
                return BadRequest("subjectOfferingId is required — send it in the request body or as a query parameter.");
            var profileClaim = User.Claims.FirstOrDefault(c => c.Type == "ProfileId");
            if (profileClaim == null) return Unauthorized("ProfileId claim not found.");
            var doctorId = Ulid.Parse(profileClaim.Value);

            var result = await examService.CreateExamAsync(offeringId, dto, doctorId);
            return CreatedAtAction(nameof(GetExam), new { id = result.Id }, result);
        }

        [HttpPost("generate-ai")]
        [Authorize(Roles = "Doctor,SuperAdmin")]
        public async Task<IActionResult> GenerateAiExam([FromBody] CreateAiExamRequest request)
        {
            var profileClaim = User.Claims.FirstOrDefault(c => c.Type == "ProfileId");
            if (profileClaim == null) return Unauthorized("ProfileId claim not found.");
            var doctorId = Ulid.Parse(profileClaim.Value);

            var result = await examService.GenerateAiExamAsync(request, doctorId);
            return CreatedAtAction(nameof(GetExam), new { id = result.Id }, result);
        }

        /// <summary>
        /// Upload a lecture PDF and get back AI-generated questions (preview only — no exam created).
        /// The frontend can let the doctor review/edit before calling POST /api/Exams to create the actual exam.
        /// </summary>
        [HttpPost("preview-questions-from-pdf")]
        [Authorize(Roles = "Doctor,SuperAdmin")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> PreviewQuestionsFromPdf([FromForm] PreviewQuestionsFromPdfRequest req)
        {
            if (req.File == null || req.File.Length == 0)
                return BadRequest("PDF file is required.");

            string pdfText;
            try
            {
                using var ms = new System.IO.MemoryStream();
                await req.File.CopyToAsync(ms);
                ms.Position = 0;
                using var pdf = UglyToad.PdfPig.PdfDocument.Open(ms.ToArray());
                var sb = new System.Text.StringBuilder();
                foreach (var page in pdf.GetPages())
                    sb.AppendLine(page.Text);
                pdfText = sb.ToString().Trim();
                if (string.IsNullOrWhiteSpace(pdfText))
                    pdfText = $"Lecture file: {req.File.FileName}";
            }
            catch
            {
                pdfText = $"Lecture file: {req.File.FileName}";
            }

            var request = new AiGenerateExamRequestDto
            {
                Subject       = req.File.FileName,
                Difficulty    = req.Difficulty,
                QuestionCount = req.QuestionCount,
                ExamType      = req.ExamType,
                Topics        = new List<string> { pdfText.Length > 2000 ? pdfText[..2000] : pdfText }
            };

            var questions = await aiService.GenerateExamAsync(request);
            return Ok(questions);
        }

        [HttpGet("{id}")]
        [Authorize(Roles = "Doctor,Student,Admin,SuperAdmin")]
        public async Task<IActionResult> GetExam(string id)
        {
            if (!Ulid.TryParse(id, out var examId)) return BadRequest("Invalid Exam ID.");
            var userRole = User.FindFirstValue(ClaimTypes.Role) ?? "Student";

            // Extract userId based on role: ProfileId for Doctor/Student, nameid for Admin
            string claimType = (userRole == "Doctor" || userRole == "Student") ? "ProfileId" : "nameid";
            var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == claimType);

            if (userIdClaim == null)
                return Unauthorized($"{claimType} claim not found.");

            var userId = Ulid.Parse(userIdClaim.Value);

            var result = await examService.GetExamByIdAsync(examId, userId, userRole);
            return Ok(result);
        }

        [HttpPost("{id}/submit")]
        [Authorize(Roles = "Student,SuperAdmin")]
        public async Task<IActionResult> SubmitExam(string id, [FromBody] ExamSubmissionDto dto)
        {
            if (!Ulid.TryParse(id, out var examId)) return BadRequest("Invalid Exam ID.");
            var profileClaim = User.Claims.FirstOrDefault(c => c.Type == "ProfileId");
            if (profileClaim == null) return Unauthorized("ProfileId claim not found.");
            var studentId = Ulid.Parse(profileClaim.Value);

            // Ensure route ID matches DTO ID or just override it
            if (dto.ExamId != Ulid.Empty && dto.ExamId != examId)
                return BadRequest("Exam ID mismatch.");

            dto.ExamId = examId;

            var submissionId = await examService.SubmitExamAsync(examId, studentId, dto);
            return CreatedAtAction(nameof(GetExam), new { id }, new { submissionId, message = "Exam submitted successfully." });
        }
        [HttpGet("{id}/my-variant")]
        [Authorize(Roles = "Student,SuperAdmin")]
        public async Task<IActionResult> GetMyVariant(string id)
        {
            if (!Ulid.TryParse(id, out var examId)) return BadRequest("Invalid Exam ID.");
            var profileClaim = User.Claims.FirstOrDefault(c => c.Type == "ProfileId");
            if (profileClaim == null) return Unauthorized("ProfileId claim not found.");
            var studentId = Ulid.Parse(profileClaim.Value);

            var result = await examService.GetStudentVariantAsync(examId, studentId);
            return Ok(result);
        }

        [HttpGet("my-exams")]
        [Authorize(Roles = "Doctor,SuperAdmin")]
        public async Task<IActionResult> GetMyExams()
        {
            var profileClaim = User.Claims.FirstOrDefault(c => c.Type == "ProfileId");
            if (profileClaim == null) return Unauthorized("ProfileId claim not found.");
            var doctorId = Ulid.Parse(profileClaim.Value);

            var exams = await examService.GetExamsByDoctorAsync(doctorId);
            return Ok(exams);
        }

        [HttpGet("by-offering/{offeringId}")]
        [Authorize(Roles = "Doctor,SuperAdmin")]
        public async Task<IActionResult> GetExamsByOffering(string offeringId)
        {
            if (!Ulid.TryParse(offeringId, out var oId)) return BadRequest("Invalid Offering ID.");
            var profileClaim = User.Claims.FirstOrDefault(c => c.Type == "ProfileId");
            if (profileClaim == null) return Unauthorized("ProfileId claim not found.");
            var doctorId = Ulid.Parse(profileClaim.Value);

            var exams = await examService.GetExamsByOfferingAsync(oId, doctorId);
            return Ok(exams);
        }

        [HttpGet("{id}/results")]
        [Authorize(Roles = "Doctor,SuperAdmin")]
        public async Task<IActionResult> GetExamResults(string id)
        {
            if (!Ulid.TryParse(id, out var examId)) return BadRequest("Invalid Exam ID.");
            var profileClaim = User.Claims.FirstOrDefault(c => c.Type == "ProfileId");
            if (profileClaim == null) return Unauthorized("ProfileId claim not found.");
            var doctorId = Ulid.Parse(profileClaim.Value);

            var submissions = await examService.GetExamSubmissionsAsync(examId, doctorId);
            return Ok(submissions);
        }

        [HttpGet("my-enrolled-exams")]
        [Authorize(Roles = "Student,SuperAdmin")]
        public async Task<IActionResult> GetMyEnrolledExams()
        {
            var profileClaim = User.Claims.FirstOrDefault(c => c.Type == "ProfileId");
            if (profileClaim == null) return Unauthorized("ProfileId claim not found.");
            var studentId = Ulid.Parse(profileClaim.Value);

            var exams = await examService.GetStudentEnrolledExamsAsync(studentId);
            return Ok(exams);
        }

        [HttpGet("{id}/my-submission")]
        [Authorize(Roles = "Student,SuperAdmin")]
        public async Task<IActionResult> GetMySubmission(string id)
        {
            if (!Ulid.TryParse(id, out var examId)) return BadRequest("Invalid Exam ID.");
            var profileClaim = User.Claims.FirstOrDefault(c => c.Type == "ProfileId");
            if (profileClaim == null) return Unauthorized("ProfileId claim not found.");
            var studentId = Ulid.Parse(profileClaim.Value);

            var submission = await examService.GetStudentSubmissionAsync(examId, studentId);

            if (submission == null)
                return NotFound("No submission found for this exam.");

            return Ok(submission);
        }

        [HttpPost("grade-submission")]
        [Authorize(Roles = "Doctor,SuperAdmin")]
        public async Task<IActionResult> GradeSubmission([FromBody] GradeSubmissionDto dto)
        {
            var profileClaim = User.Claims.FirstOrDefault(c => c.Type == "ProfileId");
            if (profileClaim == null) return Unauthorized("ProfileId claim not found.");
            var doctorId = Ulid.Parse(profileClaim.Value);

            await examService.GradeSubmissionAsync(dto, doctorId);
            return Ok(new { message = "Submission graded successfully." });
        }

        // ── Save Progress (auto-save during exam) ────────────────────────────
        [HttpPost("{id}/save-progress")]
        [Authorize(Roles = "Student,SuperAdmin")]
        public async Task<IActionResult> SaveProgress(string id, [FromBody] SaveProgressDto dto)
        {
            if (!Ulid.TryParse(id, out var examId)) return BadRequest("Invalid Exam ID.");
            var profileClaim = User.Claims.FirstOrDefault(c => c.Type == "ProfileId");
            if (profileClaim == null) return Unauthorized("ProfileId claim not found.");
            var studentId = Ulid.Parse(profileClaim.Value);

            var result = await examService.SaveProgressAsync(examId, studentId, dto);
            return Ok(result);
        }

        // ── Get Exam Session (student opens exam with state) ─────────────────
        [HttpGet("{id}/session")]
        [Authorize(Roles = "Student,SuperAdmin")]
        public async Task<IActionResult> GetExamSession(string id)
        {
            if (!Ulid.TryParse(id, out var examId)) return BadRequest("Invalid Exam ID.");
            var profileClaim = User.Claims.FirstOrDefault(c => c.Type == "ProfileId");
            if (profileClaim == null) return Unauthorized("ProfileId claim not found.");
            var studentId = Ulid.Parse(profileClaim.Value);

            var session = await examService.GetExamSessionAsync(examId, studentId);
            return Ok(session);
        }

        // ── Exam Analytics (doctor) ──────────────────────────────────────────
        [HttpGet("{id}/analytics")]
        [Authorize(Roles = "Doctor,SuperAdmin")]
        public async Task<IActionResult> GetExamAnalytics(string id)
        {
            if (!Ulid.TryParse(id, out var examId)) return BadRequest("Invalid Exam ID.");
            var profileClaim = User.Claims.FirstOrDefault(c => c.Type == "ProfileId");
            if (profileClaim == null) return Unauthorized("ProfileId claim not found.");
            var doctorId = Ulid.Parse(profileClaim.Value);

            var analytics = await examService.GetExamAnalyticsAsync(examId, doctorId);
            return Ok(analytics);
        }

        // ── LEGACY: Restore by internal ULID ──────────────────────────────────
        [HttpPost("{id}/restore")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        [ApiExplorerSettings(GroupName = "legacy")]
        public async Task<IActionResult> RestoreExam(string id)
        {
            if (!Ulid.TryParse(id, out var examId)) return BadRequest("Invalid Exam ID.");
            await examService.RestoreExamAsync(examId);
            return NoContent();
        }

        /// <summary>
        /// [PREFERRED] Restore a deleted exam by its public Code.
        /// Admin/frontend MUST use this route.
        /// </summary>
        [HttpPost("by-code/{code}/restore")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<IActionResult> RestoreExamByCode(string code)
        {
            var userRole  = User.FindFirstValue(ClaimTypes.Role) ?? "Admin";
            var userIdStr = User.Claims.FirstOrDefault(c => c.Type == "ProfileId")?.Value
                         ?? User.Claims.FirstOrDefault(c => c.Type == "nameid")?.Value;
            if (userIdStr == null) return Unauthorized("User ID claim not found.");
            var userId = Ulid.Parse(userIdStr);

            var exam = await examService.GetExamByCodeAsync(code, userId, userRole);
            if (exam == null) return NotFound($"Exam with code '{code}' not found.");

            await examService.RestoreExamAsync(exam.Id);
            return NoContent();
        }

        // ── LEGACY: Admin update/delete/restore by internal ULID ─────────────────
        [HttpPut("{id}")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        [ApiExplorerSettings(GroupName = "legacy")]
        public async Task<ActionResult<ExamDto>> UpdateExam(string id, UpdateExamDto dto)
        {
            if (!Ulid.TryParse(id, out var examId)) return BadRequest("Invalid Exam ID.");
            var result = await examService.UpdateExamAsync(examId, dto);
            return Ok(result);
        }

        // ── PREFERRED: Admin routes using public Code ─────────────────────────
        /// <summary>
        /// [PREFERRED] Update an exam by its public Code.
        /// Admin/frontend MUST use this route.
        /// </summary>
        [HttpPut("by-code/{code}")]
        [Authorize(Roles = "Admin,SuperAdmin,Doctor")]
        public async Task<ActionResult<ExamDto>> UpdateExamByCode(string code, UpdateExamDto dto)
        {
            var userRole   = User.FindFirstValue(ClaimTypes.Role) ?? "Admin";
            var userIdStr  = User.Claims.FirstOrDefault(c => c.Type == "nameid")?.Value;
            if (userIdStr == null) return Unauthorized("nameid claim not found.");
            var userId = Ulid.Parse(userIdStr);

            var exam = await examService.GetExamByCodeAsync(code, userId, userRole);
            if (exam == null) return NotFound($"Exam with code '{code}' not found.");

            var result = await examService.UpdateExamAsync(exam.Id, dto);
            return Ok(result);
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        [ApiExplorerSettings(GroupName = "legacy")]
        public async Task<IActionResult> DeleteExam(string id)
        {
            if (!Ulid.TryParse(id, out var examId)) return BadRequest("Invalid Exam ID.");
            await examService.DeleteExamAsync(examId);
            return NoContent();
        }

        /// <summary>
        /// [PREFERRED] Delete an exam by its public Code.
        /// Admin/frontend MUST use this route.
        /// </summary>
        [HttpDelete("by-code/{code}")]
        [Authorize(Roles = "Admin,SuperAdmin,Doctor")]
        public async Task<IActionResult> DeleteExamByCode(string code)
        {
            var userRole  = User.FindFirstValue(ClaimTypes.Role) ?? "Admin";
            var userIdStr = User.Claims.FirstOrDefault(c => c.Type == "ProfileId")?.Value
                         ?? User.Claims.FirstOrDefault(c => c.Type == "nameid")?.Value;
            if (userIdStr == null) return Unauthorized("User ID claim not found.");
            var userId = Ulid.Parse(userIdStr);

            var exam = await examService.GetExamByCodeAsync(code, userId, userRole);
            if (exam == null) return NotFound($"Exam with code '{code}' not found.");

            await examService.DeleteExamAsync(exam.Id);
            return NoContent();
        }

        [HttpPost("{id}/auto-grade")]
        [Authorize(Roles = "Doctor,SuperAdmin")]
        public async Task<IActionResult> AutoGradeExam(string id)
        {
            if (!Ulid.TryParse(id, out var examId)) return BadRequest("Invalid Exam ID.");
            var profileClaim = User.Claims.FirstOrDefault(c => c.Type == "ProfileId");
            if (profileClaim == null) return Unauthorized("ProfileId claim not found.");
            var doctorId = Ulid.Parse(profileClaim.Value);

            var count = await examService.AutoGradeExamAsync(examId, doctorId);
            return Ok(new { message = $"Auto-grading complete. {count} submissions processed." });
        }
    }
}
