using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
        public async Task<ActionResult<IEnumerable<SemesterDto>>> GetByYear(int academicYearId)
        {
            var list = await _semesterService.GetByAcademicYearAsync(academicYearId);
            return Ok(list);
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<SemesterDto>> Update(int id, UpdateSemesterDto dto)
        {
            var result = await _semesterService.UpdateAsync(id, dto);
            return Ok(result);
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int id)
        {
            await _semesterService.DeleteAsync(id);
            return NoContent();
        }
    }
}
