using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using UniversityManagementSystem.Core.DTOs;
using UniversityManagementSystem.Core.Entities;
using UniversityManagementSystem.Core.Interfaces;
using NUlid;

namespace UniversityManagementSystem.Api.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class EnrollmentsController(IEnrollmentService service) : ControllerBase
    {
        private readonly IEnrollmentService _service = service;

        [HttpPost("{offeringId}")]
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> Enroll(string offeringId)
        {
            if (!Ulid.TryParse(offeringId, out var oId)) return BadRequest("Invalid Offering ID.");
            var profileIdClaim = User.FindFirst("ProfileId");
            if (profileIdClaim == null) return Unauthorized("Student profile not found.");

            Ulid currentStudentId = Ulid.Parse(profileIdClaim.Value);

            var dto = new CreateEnrollmentDto(currentStudentId, oId);

            await _service.EnrollStudentAsync(dto);
            return Ok(new { Message = "Enrolled successfully." });
        }

        [HttpGet("my-enrollments")]
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> GetMyEnrollments()
        {
            var profileIdClaim = User.FindFirst("ProfileId");
            if (profileIdClaim == null) return Unauthorized("Student profile not found.");
            Ulid studentId = Ulid.Parse(profileIdClaim.Value);

            var enrollments = await _service.GetStudentEnrollmentsAsync(studentId);

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

        [HttpGet("by-offering/{offeringId}")]
        [Authorize(Roles = "Doctor,Admin")]
        public async Task<IActionResult> GetByOffering(string offeringId)
        {
            if (!Ulid.TryParse(offeringId, out var oId)) return BadRequest("Invalid Offering ID.");
            var enrollments = await _service.GetEnrollmentsByOfferingIdAsync(oId);

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

        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(string id)
        {
            if (!Ulid.TryParse(id, out var enrollmentId)) return BadRequest("Invalid Enrollment ID.");
            await _service.UnenrollStudentAsync(enrollmentId);
            return NoContent();
        }

        [HttpPut("{id}/reactivate")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<IActionResult> Reactivate(string id)
        {
            if (!Ulid.TryParse(id, out var enrollmentId)) return BadRequest("Invalid Enrollment ID.");
            await _service.ReactivateEnrollmentAsync(enrollmentId);
            return Ok(new { Message = "Enrollment reactivated successfully." });
        }
    }
}
