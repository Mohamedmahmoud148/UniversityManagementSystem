using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniversityManagementSystem.Core.DTOs;
using UniversityManagementSystem.Core.Interfaces;

namespace UniversityManagementSystem.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ExamsController(IExamService examService) : ControllerBase
    {

        [HttpPost]
        [Authorize(Roles = "Doctor")]
        public async Task<IActionResult> CreateExam([FromQuery] int subjectOfferingId, [FromBody] CreateExamDto dto)
        {
            var profileClaim = User.Claims.FirstOrDefault(c => c.Type == "ProfileId");
            if (profileClaim == null) return Unauthorized("ProfileId claim not found.");
            var doctorId = int.Parse(profileClaim.Value);

            var result = await examService.CreateExamAsync(subjectOfferingId, dto, doctorId);
            return CreatedAtAction(nameof(GetExam), new { id = result.Id }, result);
        }

        [HttpGet("{id}")]
        [Authorize(Roles = "Doctor,Student,Admin,SuperAdmin")]
        public async Task<IActionResult> GetExam(int id)
        {
            var userRole = User.FindFirstValue(ClaimTypes.Role) ?? "Student";
            
            // Extract userId based on role: ProfileId for Doctor/Student, nameid for Admin
            string claimType = (userRole == "Doctor" || userRole == "Student") ? "ProfileId" : "nameid";
            var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == claimType);

            if (userIdClaim == null) 
                return Unauthorized($"{claimType} claim not found.");

            var userId = int.Parse(userIdClaim.Value);

            var result = await examService.GetExamByIdAsync(id, userId, userRole);
            return Ok(result);
        }

        [HttpPost("{id}/submit")]
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> SubmitExam(int id, [FromBody] ExamSubmissionDto dto)
        {
            var profileClaim = User.Claims.FirstOrDefault(c => c.Type == "ProfileId");
            if (profileClaim == null) return Unauthorized("ProfileId claim not found.");
            var studentId = int.Parse(profileClaim.Value);

            // Ensure route ID matches DTO ID or just override it
            if (dto.ExamId != 0 && dto.ExamId != id)
                return BadRequest("Exam ID mismatch.");
            
            dto.ExamId = id;

            var submissionId = await examService.SubmitExamAsync(id, studentId, dto);
            return CreatedAtAction(nameof(GetExam), new { id }, new { submissionId, message = "Exam submitted successfully." });
        }
        [HttpGet("my-exams")]
        [Authorize(Roles = "Doctor")]
        public async Task<IActionResult> GetMyExams()
        {
            var profileClaim = User.Claims.FirstOrDefault(c => c.Type == "ProfileId");
            if (profileClaim == null) return Unauthorized("ProfileId claim not found.");
            var doctorId = int.Parse(profileClaim.Value);

            var exams = await examService.GetExamsByDoctorAsync(doctorId);
            return Ok(exams);
        }

        [HttpGet("by-offering/{offeringId}")]
        [Authorize(Roles = "Doctor")]
        public async Task<IActionResult> GetExamsByOffering(int offeringId)
        {
            var profileClaim = User.Claims.FirstOrDefault(c => c.Type == "ProfileId");
            if (profileClaim == null) return Unauthorized("ProfileId claim not found.");
            var doctorId = int.Parse(profileClaim.Value);

            var exams = await examService.GetExamsByOfferingAsync(offeringId, doctorId);
            return Ok(exams);
        }

        [HttpGet("{id}/results")]
        [Authorize(Roles = "Doctor")]
        public async Task<IActionResult> GetExamResults(int id)
        {
            var profileClaim = User.Claims.FirstOrDefault(c => c.Type == "ProfileId");
            if (profileClaim == null) return Unauthorized("ProfileId claim not found.");
            var doctorId = int.Parse(profileClaim.Value);

            var submissions = await examService.GetExamSubmissionsAsync(id, doctorId);
            return Ok(submissions);
        }

        [HttpGet("my-enrolled-exams")]
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> GetMyEnrolledExams()
        {
            var profileClaim = User.Claims.FirstOrDefault(c => c.Type == "ProfileId");
            if (profileClaim == null) return Unauthorized("ProfileId claim not found.");
            var studentId = int.Parse(profileClaim.Value);

            var exams = await examService.GetStudentEnrolledExamsAsync(studentId);
            return Ok(exams);
        }

        [HttpGet("{id}/my-submission")]
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> GetMySubmission(int id)
        {
            var profileClaim = User.Claims.FirstOrDefault(c => c.Type == "ProfileId");
            if (profileClaim == null) return Unauthorized("ProfileId claim not found.");
            var studentId = int.Parse(profileClaim.Value);

            var submission = await examService.GetStudentSubmissionAsync(id, studentId);
            
            if (submission == null)
                return NotFound("No submission found for this exam.");

            return Ok(submission);
        }

        [HttpPost("grade-submission")]
        [Authorize(Roles = "Doctor")]
        public async Task<IActionResult> GradeSubmission([FromBody] GradeSubmissionDto dto)
        {
            var profileClaim = User.Claims.FirstOrDefault(c => c.Type == "ProfileId");
            if (profileClaim == null) return Unauthorized("ProfileId claim not found.");
            var doctorId = int.Parse(profileClaim.Value);

            await examService.GradeSubmissionAsync(dto, doctorId);
            return Ok(new { message = "Submission graded successfully." });
        }

        [HttpPost("{id}/restore")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> RestoreExam(int id)
        {
            await examService.RestoreExamAsync(id);
            return NoContent();
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ExamDto>> UpdateExam(int id, UpdateExamDto dto)
        {
            var result = await examService.UpdateExamAsync(id, dto);
            return Ok(result);
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteExam(int id)
        {
            await examService.DeleteExamAsync(id);
            return NoContent();
        }

        [HttpPost("{id}/auto-grade")]
        [Authorize(Roles = "Doctor")]
        public async Task<IActionResult> AutoGradeExam(int id)
        {
            var profileClaim = User.Claims.FirstOrDefault(c => c.Type == "ProfileId");
            if (profileClaim == null) return Unauthorized("ProfileId claim not found.");
            var doctorId = int.Parse(profileClaim.Value);

            var count = await examService.AutoGradeExamAsync(id, doctorId);
            return Ok(new { message = $"Auto-grading complete. {count} submissions processed." });
        }
    }
}
