using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NUlid;
using UniversityManagementSystem.Core.DTOs;
using UniversityManagementSystem.Core.Interfaces;

namespace UniversityManagementSystem.Api.Controllers
{
    [ApiController]
    [Route("api/academic-years")]
    [Authorize]
    public class AcademicYearsController(
        IAcademicYearService academicYearService,
        IAcademicYearDepartmentService departmentMappingService) : ControllerBase
    {
        private readonly IAcademicYearService _service = academicYearService;
        private readonly IAcademicYearDepartmentService _mapping = departmentMappingService;

        // ════════════════════════════════════════════════════════════════════════
        // ACADEMIC YEAR CRUD
        // ════════════════════════════════════════════════════════════════════════

        /// <summary>Create a new academic year for a college (Admin only).</summary>
        [HttpPost]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<IActionResult> Create([FromBody] CreateAcademicYearDto dto)
        {
            var result = await _service.CreateAsync(dto);
            return CreatedAtAction(nameof(GetAll), new { id = result.Id }, result);
        }

        /// <summary>List all academic years across all colleges, ordered by college then order.</summary>
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var result = await _service.GetAllAsync();
            return Ok(result);
        }

        /// <summary>Activate this year (deactivates all others in the same college) (Admin only).</summary>
        [HttpPost("{id}/activate")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<IActionResult> Activate(string id)
        {
            if (!Ulid.TryParse(id, out var uid)) return BadRequest("Invalid Academic Year ID.");
            await _service.ActivateAsync(uid);
            return NoContent();
        }

        /// <summary>Update name, isActive, or order of an academic year (Admin only).</summary>
        [HttpPut("{id}")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<IActionResult> Update(string id, [FromBody] UpdateAcademicYearDto dto)
        {
            if (!Ulid.TryParse(id, out var uid)) return BadRequest("Invalid Academic Year ID.");
            var result = await _service.UpdateAsync(uid, dto);
            return Ok(result);
        }

        /// <summary>Soft-delete an academic year (Admin only). Fails if it has associated semesters.</summary>
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<IActionResult> Delete(string id)
        {
            if (!Ulid.TryParse(id, out var uid)) return BadRequest("Invalid Academic Year ID.");
            await _service.DeleteAsync(uid);
            return NoContent();
        }

        // ════════════════════════════════════════════════════════════════════════
        // DEPARTMENT MAPPINGS
        // ════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// GET /api/academic-years/{yearId}/departments
        /// Returns only ACTIVE department mappings for the given year.
        /// Available to all authenticated users (students, doctors, admins).
        /// </summary>
        [HttpGet("{yearId}/departments")]
        public async Task<IActionResult> GetActiveDepartments(string yearId)
        {
            if (!Ulid.TryParse(yearId, out var uid)) return BadRequest("Invalid Academic Year ID.");
            var result = await _mapping.GetActiveDepartmentsForYearAsync(uid);
            return Ok(result);
        }

        /// <summary>
        /// GET /api/academic-years/{yearId}/departments/all
        /// Returns ALL mappings (active + inactive) for admin management views.
        /// </summary>
        [HttpGet("{yearId}/departments/all")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<IActionResult> GetAllMappings(string yearId)
        {
            if (!Ulid.TryParse(yearId, out var uid)) return BadRequest("Invalid Academic Year ID.");
            var result = await _mapping.GetAllMappingsForYearAsync(uid);
            return Ok(result);
        }

        /// <summary>
        /// POST /api/academic-years/{yearId}/departments
        /// Assign a department to an academic year.
        /// Enforces: Department.CollegeId == AcademicYear.CollegeId.
        /// </summary>
        [HttpPost("{yearId}/departments")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<IActionResult> AssignDepartment(string yearId, [FromBody] AssignDepartmentToYearDto dto)
        {
            if (!Ulid.TryParse(yearId, out var uid)) return BadRequest("Invalid Academic Year ID.");
            var result = await _mapping.AssignDepartmentAsync(uid, dto);
            return Ok(result);
        }

        /// <summary>
        /// PATCH /api/academic-years/{yearId}/departments/{mappingId}
        /// Toggle the IsActive flag on an existing mapping (enable/disable a department for a year).
        /// </summary>
        [HttpPatch("{yearId}/departments/{mappingId}")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<IActionResult> UpdateMapping(string yearId, string mappingId, [FromBody] UpdateYearDepartmentDto dto)
        {
            if (!Ulid.TryParse(yearId, out _)) return BadRequest("Invalid Academic Year ID.");
            if (!Ulid.TryParse(mappingId, out var mId)) return BadRequest("Invalid Mapping ID.");
            await _mapping.UpdateMappingAsync(mId, dto.IsActive);
            return NoContent();
        }

        /// <summary>
        /// DELETE /api/academic-years/{yearId}/departments/{mappingId}
        /// Permanently removes a mapping (hard delete).
        /// </summary>
        [HttpDelete("{yearId}/departments/{mappingId}")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<IActionResult> RemoveMapping(string yearId, string mappingId)
        {
            if (!Ulid.TryParse(yearId, out _)) return BadRequest("Invalid Academic Year ID.");
            if (!Ulid.TryParse(mappingId, out var mId)) return BadRequest("Invalid Mapping ID.");
            await _mapping.RemoveMappingAsync(mId);
            return NoContent();
        }
    }
}
