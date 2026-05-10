using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NUlid;
using UniversityManagementSystem.Core.DTOs;
using UniversityManagementSystem.Core.Interfaces;

namespace UniversityManagementSystem.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SubjectOfferingsController(ISubjectOfferingService service, IEnrollmentService enrollmentService) : ControllerBase
    {
        private readonly ISubjectOfferingService _service = service;
        private readonly IEnrollmentService _enrollmentService = enrollmentService;

        [HttpGet("by-code/{code}")]
        public async Task<IActionResult> GetByCode(string code)
        {
            var result = await _service.GetByCodeAsync(code);
            if (result == null) return NotFound();
            return Ok(result);
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create([FromBody] CreateSubjectOfferingDto dto)
        {
            var result = await _service.CreateAsync(dto);
            return CreatedAtAction(nameof(GetBySemester), new { semesterId = result.SemesterId }, result);
        }

        [HttpGet("by-semester/{semesterId}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetBySemester(string semesterId)
        {
            if (!Ulid.TryParse(semesterId, out var uid)) return BadRequest("Invalid Semester ID.");
            var result = await _service.GetBySemesterAsync(uid);
            return Ok(result);
        }

        [HttpGet("my-offerings")]
        [Authorize(Roles = "Doctor")]
        public async Task<IActionResult> GetMyOfferings()
        {
            var claim = User.Claims.FirstOrDefault(c => c.Type == "nameid");
            if (claim == null) return Unauthorized("User ID claim not found.");
            if (!Ulid.TryParse(claim.Value, out var userId)) return Unauthorized("Invalid user ID.");
            var result = await _service.GetByDoctorAsync(userId);
            return Ok(result);
        }

        [HttpGet("my-enrollments")]
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> GetMyEnrollments()
        {
            var profileIdClaim = User.FindFirst("ProfileId");
            if (profileIdClaim == null) return Unauthorized("Student profile not found.");
            if (!Ulid.TryParse(profileIdClaim.Value, out var studentId)) return Unauthorized("Invalid student ID.");
            
            var enrollments = await _enrollmentService.GetStudentEnrollmentsAsync(studentId);

            var dtos = enrollments.Select(e => new EnrollmentDto
            {
                Id = e.Id,
                StudentId = e.StudentId,
                StudentName = e.Student?.FullName ?? string.Empty,
                SubjectOfferingId = e.SubjectOfferingId,
                SubjectCode = e.SubjectOffering?.Subject?.Code ?? string.Empty,
                SubjectName = e.SubjectOffering?.Subject?.Name ?? string.Empty,
                DepartmentName = e.SubjectOffering?.Department?.Name ?? string.Empty,
                DoctorName = e.SubjectOffering?.Doctor?.FullName ?? string.Empty,
                SemesterName = e.SubjectOffering?.Semester?.Name ?? string.Empty,
                EnrolledAt = e.EnrolledAt,
                IsActive = e.IsActive
            });

            return Ok(dtos);
        }
    }
}
