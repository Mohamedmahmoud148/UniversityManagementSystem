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
    public class SubjectOfferingsController(ISubjectOfferingService service) : ControllerBase
    {
        private readonly ISubjectOfferingService _service = service;

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create([FromBody] CreateSubjectOfferingDto dto)
        {
            var result = await _service.CreateAsync(dto);
            return CreatedAtAction(nameof(GetBySemester), new { semesterId = result.SemesterId }, result);
        }

        [HttpGet("by-semester/{semesterId}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetBySemester(int semesterId)
        {
            var result = await _service.GetBySemesterAsync(semesterId);
            return Ok(result);
        }

        [HttpGet("my-offerings")]
        [Authorize(Roles = "Doctor")]
        public async Task<IActionResult> GetMyOfferings()
        {
            var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == "nameid");
            if (userIdClaim == null) return Unauthorized("User ID claim not found.");
            var userId = int.Parse(userIdClaim.Value);

            var result = await _service.GetByDoctorAsync(userId);
            return Ok(result);
        }

        [HttpGet("my-enrollments")]
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> GetMyEnrollments()
        {
            var profileIdClaim = User.FindFirst("ProfileId");
            if (profileIdClaim == null) return Unauthorized("Student profile not found.");

            int currentStudentId = int.Parse(profileIdClaim.Value);

            var result = await _service.GetByStudentAsync(currentStudentId);
            return Ok(result);
        }
    }
}
