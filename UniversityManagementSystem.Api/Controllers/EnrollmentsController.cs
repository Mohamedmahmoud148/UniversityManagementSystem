using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using UniversityManagementSystem.Core.DTOs;
using UniversityManagementSystem.Core.Entities;
using UniversityManagementSystem.Core.Interfaces;

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
        public async Task<IActionResult> Enroll(int offeringId)
        {
            var profileIdClaim = User.FindFirst("ProfileId");
            if (profileIdClaim == null) return Unauthorized("Student profile not found.");

            int currentStudentId = int.Parse(profileIdClaim.Value);
            
            var dto = new CreateEnrollmentDto(currentStudentId, offeringId);

            await _service.EnrollStudentAsync(dto);
            return Ok(new { Message = "Enrolled successfully." });
        }

        [HttpGet("my-enrollments")]
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> GetMyEnrollments()
        {
            var profileIdClaim = User.FindFirst("ProfileId");
            if (profileIdClaim == null) return Unauthorized("Student profile not found.");
            int studentId = int.Parse(profileIdClaim.Value);

            var enrollments = await _service.GetStudentEnrollmentsAsync(studentId);
            
            var dtos = enrollments.Select(e => new EnrollmentDto
            {
                Id = e.Id,
                StudentId = e.StudentId,
                StudentName = e.Student?.FullName ?? string.Empty,
                SubjectOfferingId = e.SubjectOfferingId,
                SubjectName = e.SubjectOffering?.Subject?.Name ?? string.Empty,
                DoctorName = e.SubjectOffering?.Doctor?.FullName ?? string.Empty,
                SemesterName = e.SubjectOffering?.Semester?.Name ?? string.Empty,
                EnrolledAt = e.EnrolledAt,
                IsActive = e.IsActive
            });

            return Ok(dtos); 
        }

        [HttpGet("by-offering/{offeringId}")]
        [Authorize(Roles = "Doctor,Admin")]
        public async Task<IActionResult> GetByOffering(int offeringId)
        {
            var enrollments = await _service.GetEnrollmentsByOfferingIdAsync(offeringId);

            var dtos = enrollments.Select(e => new EnrollmentDto
            {
                Id = e.Id,
                StudentId = e.StudentId,
                StudentName = e.Student?.FullName ?? string.Empty,
                SubjectOfferingId = e.SubjectOfferingId,
                SubjectName = e.SubjectOffering?.Subject?.Name ?? string.Empty,
                DoctorName = e.SubjectOffering?.Doctor?.FullName ?? string.Empty,
                SemesterName = e.SubjectOffering?.Semester?.Name ?? string.Empty,
                EnrolledAt = e.EnrolledAt,
                IsActive = e.IsActive
            });

            return Ok(dtos);
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int id)
        {
            await _service.UnenrollStudentAsync(id);
            await _service.UnenrollStudentAsync(id);
            return NoContent();
        }

        [HttpPut("{id}/reactivate")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<IActionResult> Reactivate(int id)
        {
            await _service.ReactivateEnrollmentAsync(id);
            return Ok(new { Message = "Enrollment reactivated successfully." });
        }
    }
}
