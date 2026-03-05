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
    public class AcademicYearsController : ControllerBase
    {
        private readonly IAcademicYearService _academicYearService;

        public AcademicYearsController(IAcademicYearService academicYearService)
        {
            _academicYearService = academicYearService;
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateAcademicYearDto dto)
        {
            var result = await _academicYearService.CreateAsync(dto);
            return CreatedAtAction(nameof(GetAll), new { id = result.Id }, result);
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var result = await _academicYearService.GetAllAsync();
            return Ok(result);
        }

        [HttpPost("activate/{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Activate(string id)
        {
            if (!Ulid.TryParse(id, out var uid)) return BadRequest("Invalid ID.");
            await _academicYearService.ActivateAsync(uid);
            return NoContent();
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<AcademicYearDto>> Update(string id, UpdateAcademicYearDto dto)
        {
            if (!Ulid.TryParse(id, out var uid)) return BadRequest("Invalid ID.");
            var result = await _academicYearService.UpdateAsync(uid, dto);
            return Ok(result);
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(string id)
        {
            if (!Ulid.TryParse(id, out var uid)) return BadRequest("Invalid ID.");
            await _academicYearService.DeleteAsync(uid);
            return NoContent();
        }
    }
}
