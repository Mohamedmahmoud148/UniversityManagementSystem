using System.Collections.Generic;
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
    [Authorize(Roles = "Admin")]
    public class SemestersController : ControllerBase
    {
        private readonly ISemesterService _semesterService;

        public SemestersController(ISemesterService semesterService)
        {
            _semesterService = semesterService;
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateSemesterDto dto)
        {
            var result = await _semesterService.CreateAsync(dto);
            return CreatedAtAction(nameof(GetByYear), new { academicYearId = result.AcademicYearId }, result);
        }

        [HttpGet("by-academic-year/{academicYearId}")]
        public async Task<ActionResult<IEnumerable<SemesterDto>>> GetByYear(string academicYearId)
        {
            if (!Ulid.TryParse(academicYearId, out var uid)) return BadRequest("Invalid Academic Year ID.");
            var list = await _semesterService.GetByAcademicYearAsync(uid);
            return Ok(list);
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<SemesterDto>> Update(string id, UpdateSemesterDto dto)
        {
            if (!Ulid.TryParse(id, out var uid)) return BadRequest("Invalid ID.");
            var result = await _semesterService.UpdateAsync(uid, dto);
            return Ok(result);
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(string id)
        {
            if (!Ulid.TryParse(id, out var uid)) return BadRequest("Invalid ID.");
            await _semesterService.DeleteAsync(uid);
            return NoContent();
        }
    }
}
