using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NUlid;
using UniversityManagementSystem.Core.DTOs;
using UniversityManagementSystem.Core.Interfaces;
using UniversityManagementSystem.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace UniversityManagementSystem.Api.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/registration")]
    public class RegistrationController(
        IRegistrationService registrationService,
        IAcademicStatusService statusService,
        IUserContextService userContext,
        AppDbContext context) : ControllerBase
    {
        // ── GET /api/registration/eligible-offerings?semesterId=xxx ──────────
        // Returns all offerings for a semester with eligibility status.
        // Green = can enroll. Red = blocked with reasons. Yellow = warnings.
        [HttpGet("eligible-offerings")]
        [Authorize(Roles = "Student")]
        public async Task<ActionResult<ApiResponse<System.Collections.Generic.IReadOnlyList<EligibleOfferingDto>>>> GetEligibleOfferings(
            [FromQuery] string semesterId)
        {
            if (!Ulid.TryParse(semesterId, out var semId))
                return BadRequest(ApiResponse<object>.FailureResponse("Invalid semester ID."));

            var studentId = userContext.GetProfileId();
            var result    = await registrationService.GetEligibleOfferingsAsync(studentId, semId);

            return Ok(ApiResponse<System.Collections.Generic.IReadOnlyList<EligibleOfferingDto>>
                .SuccessResponse(result, $"Found {result.Count} offerings."));
        }

        // ── POST /api/registration/enroll/{offeringId} ───────────────────────
        // Validates + enrolls the student. Auto-adds to waitlist if full.
        [HttpPost("enroll/{offeringId}")]
        [Authorize(Roles = "Student")]
        public async Task<ActionResult<ApiResponse<EnrollmentResultDto>>> Enroll(string offeringId)
        {
            if (!Ulid.TryParse(offeringId, out var oid))
                return BadRequest(ApiResponse<object>.FailureResponse("Invalid offering ID."));

            var studentId = userContext.GetProfileId();
            var result    = await registrationService.EnrollAsync(studentId, oid);

            if (!result.Success && !result.AddedToWaitlist)
                return Conflict(ApiResponse<EnrollmentResultDto>.FailureResponse(
                    "Registration blocked.", result.Errors));

            return Ok(ApiResponse<EnrollmentResultDto>.SuccessResponse(result, result.Message));
        }

        // ── POST /api/registration/waitlist/{offeringId} ─────────────────────
        // Manually join the waitlist (without attempting enrollment).
        [HttpPost("waitlist/{offeringId}")]
        [Authorize(Roles = "Student")]
        public async Task<ActionResult<ApiResponse<WaitlistResultDto>>> JoinWaitlist(string offeringId)
        {
            if (!Ulid.TryParse(offeringId, out var oid))
                return BadRequest(ApiResponse<object>.FailureResponse("Invalid offering ID."));

            var studentId = userContext.GetProfileId();
            var result    = await registrationService.JoinWaitlistAsync(studentId, oid);
            return Ok(ApiResponse<WaitlistResultDto>.SuccessResponse(result, result.Message));
        }

        // ── DELETE /api/registration/waitlist/{offeringId} ───────────────────
        [HttpDelete("waitlist/{offeringId}")]
        [Authorize(Roles = "Student")]
        public async Task<ActionResult<ApiResponse<object>>> LeaveWaitlist(string offeringId)
        {
            if (!Ulid.TryParse(offeringId, out var oid))
                return BadRequest(ApiResponse<object>.FailureResponse("Invalid offering ID."));

            var studentId = userContext.GetProfileId();
            await registrationService.LeaveWaitlistAsync(studentId, oid);
            return Ok(ApiResponse<object>.SuccessResponse(null!, "Removed from waitlist."));
        }

        // ── GET /api/registration/academic-status ────────────────────────────
        // GPA dashboard endpoint — returns persisted status + policy-based max hours.
        [HttpGet("academic-status")]
        [Authorize(Roles = "Student")]
        public async Task<ActionResult<ApiResponse<AcademicStatusDto>>> GetAcademicStatus()
        {
            var studentId = userContext.GetProfileId();
            var student   = await context.Students
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == studentId && s.DeletedAt == null);

            if (student == null)
                return NotFound(ApiResponse<object>.FailureResponse("Student not found."));

            var status = await statusService.GetOrCreateAsync(studentId);
            var policy = await statusService.GetPolicyAsync(student.DepartmentId);
            int maxHours = ComputeMaxHours(status.GPA, policy);

            var dto = new AcademicStatusDto
            {
                StudentId       = studentId.ToString(),
                StudentName     = student.FullName,
                GPA             = status.GPA,
                CGPA            = status.CGPA,
                LastSemesterGPA = status.LastSemesterGPA,
                Standing        = status.Standing.ToString(),
                StandingColor   = status.Standing switch
                {
                    Core.Entities.AcademicStanding.Good      => "green",
                    Core.Entities.AcademicStanding.Warning   => "yellow",
                    Core.Entities.AcademicStanding.Probation => "orange",
                    Core.Entities.AcademicStanding.Suspended => "red",
                    Core.Entities.AcademicStanding.Graduated => "blue",
                    _                                         => "gray"
                },
                EarnedHours    = status.EarnedCreditHours,
                RemainingHours = status.RemainingCreditHours,
                TotalRequired  = status.TotalRequiredHours,
                CurrentLevel   = status.CurrentLevel,
                MaxAllowedHours = maxHours,
                WarningCount   = status.WarningCount,
                HasWarning     = status.Standing >= Core.Entities.AcademicStanding.Warning,
                WarningMessage = status.Standing switch
                {
                    Core.Entities.AcademicStanding.Warning   =>
                        $"Academic warning: GPA {status.GPA:F2} is below {policy.WarningGpaThreshold}. Max {policy.WarningMaxHours} credit hours this semester.",
                    Core.Entities.AcademicStanding.Probation =>
                        $"Academic probation: GPA {status.GPA:F2} is below {policy.ProbationGpaThreshold}. Max {policy.ProbationMaxHours} credit hours. Improve GPA to avoid suspension.",
                    Core.Entities.AcademicStanding.Suspended =>
                        "Your academic enrollment is suspended. Please contact the academic affairs office.",
                    _ => null
                }
            };

            return Ok(ApiResponse<AcademicStatusDto>.SuccessResponse(dto));
        }

        // ── GET /api/registration/my-enrollments-summary ─────────────────────
        // Quick enrollment summary for demo dashboard.
        [HttpGet("my-enrollments-summary")]
        [Authorize(Roles = "Student")]
        public async Task<ActionResult<ApiResponse<object>>> GetEnrollmentSummary()
        {
            var studentId = userContext.GetProfileId();

            var enrollments = await context.Enrollments
                .AsNoTracking()
                .Include(e => e.SubjectOffering)
                    .ThenInclude(so => so.Subject)
                .Include(e => e.SubjectOffering)
                    .ThenInclude(so => so.Doctor)
                .Include(e => e.SubjectOffering)
                    .ThenInclude(so => so.Semester)
                .Where(e => e.StudentId == studentId && e.IsActive && e.DeletedAt == null)
                .ToListAsync();

            var totalHours = enrollments.Sum(e => e.SubjectOffering?.Subject?.CreditHours ?? 0);

            return Ok(ApiResponse<object>.SuccessResponse(new
            {
                totalEnrollments = enrollments.Count,
                totalCreditHours = totalHours,
                subjects = enrollments.Select(e => new
                {
                    subjectName   = e.SubjectOffering?.Subject?.Name,
                    subjectCode   = e.SubjectOffering?.Subject?.Code,
                    creditHours   = e.SubjectOffering?.Subject?.CreditHours,
                    doctorName    = e.SubjectOffering?.Doctor?.FullName,
                    semesterName  = e.SubjectOffering?.Semester?.Name,
                    enrolledAt    = e.EnrolledAt
                })
            }));
        }

        // ── Admin endpoints ───────────────────────────────────────────────────

        // GET /api/registration/prerequisites/{subjectId}
        [HttpGet("prerequisites/{subjectId}")]
        [Authorize(Roles = "Admin,SuperAdmin,Doctor")]
        public async Task<ActionResult<ApiResponse<object>>> GetPrerequisites(string subjectId)
        {
            if (!Ulid.TryParse(subjectId, out var sid))
                return BadRequest(ApiResponse<object>.FailureResponse("Invalid subject ID."));

            var prereqs = await context.SubjectPrerequisites
                .AsNoTracking()
                .Include(p => p.PrerequisiteSubject)
                .Where(p => p.SubjectId == sid && p.DeletedAt == null)
                .Select(p => new
                {
                    id             = p.Id.ToString(),
                    prerequisiteId = p.PrerequisiteSubjectId.ToString(),
                    name           = p.PrerequisiteSubject.Name,
                    code           = p.PrerequisiteSubject.Code,
                    minimumGrade   = p.MinimumGrade
                })
                .ToListAsync();

            return Ok(ApiResponse<object>.SuccessResponse(prereqs));
        }

        // POST /api/registration/prerequisites
        [HttpPost("prerequisites")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<ActionResult<ApiResponse<object>>> AddPrerequisite(
            [FromBody] AddPrerequisiteDto dto)
        {
            if (!Ulid.TryParse(dto.SubjectId, out var sid) ||
                !Ulid.TryParse(dto.PrerequisiteSubjectId, out var pid))
                return BadRequest(ApiResponse<object>.FailureResponse("Invalid IDs."));

            if (sid == pid)
                return BadRequest(ApiResponse<object>.FailureResponse("A subject cannot be its own prerequisite."));

            var exists = await context.SubjectPrerequisites
                .AnyAsync(p => p.SubjectId == sid
                            && p.PrerequisiteSubjectId == pid
                            && p.DeletedAt == null);

            if (exists)
                return Conflict(ApiResponse<object>.FailureResponse("Prerequisite already exists."));

            context.SubjectPrerequisites.Add(new Core.Entities.SubjectPrerequisite
            {
                SubjectId             = sid,
                PrerequisiteSubjectId = pid,
                MinimumGrade          = dto.MinimumGrade
            });

            await context.SaveChangesAsync();
            return Ok(ApiResponse<object>.SuccessResponse(null!, "Prerequisite added."));
        }

        // DELETE /api/registration/prerequisites/{id}
        [HttpDelete("prerequisites/{id}")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<ActionResult<ApiResponse<object>>> RemovePrerequisite(string id)
        {
            if (!Ulid.TryParse(id, out var uid))
                return BadRequest(ApiResponse<object>.FailureResponse("Invalid ID."));

            var prereq = await context.SubjectPrerequisites.FindAsync(uid);
            if (prereq == null || prereq.DeletedAt != null)
                return NotFound(ApiResponse<object>.FailureResponse("Prerequisite not found."));

            prereq.DeletedAt = System.DateTime.UtcNow;
            await context.SaveChangesAsync();
            return Ok(ApiResponse<object>.SuccessResponse(null!, "Prerequisite removed."));
        }

        // GET /api/registration/policy
        [HttpGet("policy")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<ActionResult<ApiResponse<Core.Entities.AcademicPolicy>>> GetPolicy(
            [FromQuery] string? departmentId = null)
        {
            Ulid? deptId = null;
            if (!string.IsNullOrEmpty(departmentId) && Ulid.TryParse(departmentId, out var d))
                deptId = d;

            var policy = await statusService.GetPolicyAsync(deptId);
            return Ok(ApiResponse<Core.Entities.AcademicPolicy>.SuccessResponse(policy));
        }

        private static int ComputeMaxHours(double gpa, Core.Entities.AcademicPolicy policy) => gpa switch
        {
            var g when g >= policy.HonorGpaThreshold    => policy.HonorMaxHours,
            var g when g < policy.ProbationGpaThreshold => policy.ProbationMaxHours,
            var g when g < policy.WarningGpaThreshold   => policy.WarningMaxHours,
            _                                            => policy.DefaultMaxHours
        };
    }

    public class AddPrerequisiteDto
    {
        public string SubjectId             { get; set; } = string.Empty;
        public string PrerequisiteSubjectId { get; set; } = string.Empty;
        public double? MinimumGrade         { get; set; }
    }
}
