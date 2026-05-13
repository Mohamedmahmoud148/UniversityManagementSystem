using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using NUlid;
using UniversityManagementSystem.Core.DTOs;
using UniversityManagementSystem.Core.Interfaces;

namespace UniversityManagementSystem.Api.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class AdminsController : ControllerBase
    {
        private readonly IAdminService _service;
        private readonly IUserContextService _userContext;

        public AdminsController(IAdminService service, IUserContextService userContext)
        {
            _service = service;
            _userContext = userContext;
        }

        [HttpGet]
        [Authorize(Roles = "SuperAdmin,Admin")]
        public async Task<ActionResult<IEnumerable<AdminDto>>> GetAll()
        {
            // SuperAdmin sees all admins; Admin sees only their own profile
            if (User.IsInRole("SuperAdmin"))
            {
                var list = await _service.GetAllAdminsAsync();
                return Ok(list);
            }
            else
            {
                var raw = _userContext.TryGetProfileId();
                if (!Ulid.TryParse(raw ?? string.Empty, out var profileId))
                    return Forbid();

                var admin = await _service.GetAdminByIdAsync(profileId);
                if (admin == null) return NotFound();

                return Ok(new List<AdminDto> { admin });
            }
        }

        [HttpGet("{id}")]
        [Authorize(Roles = "SuperAdmin,Admin")]
        public async Task<ActionResult<AdminDto>> GetById(string id)
        {
            if (!Ulid.TryParse(id, out var adminId)) return BadRequest("Invalid ID format");

            if (!User.IsInRole("SuperAdmin"))
            {
                if (_userContext.TryGetProfileId() != id) return Forbid();
            }

            var admin = await _service.GetAdminByIdAsync(adminId);
            if (admin == null) return NotFound();

            return Ok(admin);
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "SuperAdmin,Admin")]
        public async Task<IActionResult> Update(string id, [FromBody] UpdateAdminDto dto)
        {
            if (!Ulid.TryParse(id, out var adminId)) return BadRequest("Invalid ID format");

            if (!User.IsInRole("SuperAdmin"))
            {
                if (_userContext.TryGetProfileId() != id) return Forbid();
            }

            try
            {
                await _service.UpdateAdminAsync(adminId, dto);
                return NoContent();
            }
            catch (System.Collections.Generic.KeyNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "SuperAdmin")]
        public async Task<IActionResult> Delete(string id)
        {
            if (!Ulid.TryParse(id, out var adminId)) return BadRequest("Invalid ID format");

            try
            {
                await _service.DeleteAdminAsync(adminId);
                return NoContent();
            }
            catch (System.Collections.Generic.KeyNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
        }

        [HttpPut("{id}/activate")]
        [Authorize(Roles = "SuperAdmin")]
        public async Task<IActionResult> Activate(string id)
        {
            if (!Ulid.TryParse(id, out var adminId)) return BadRequest("Invalid ID format");

            try
            {
                await _service.ToggleAdminStatusAsync(adminId, true);
                return NoContent();
            }
            catch (System.Collections.Generic.KeyNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
        }

        [HttpPut("{id}/deactivate")]
        [Authorize(Roles = "SuperAdmin")]
        public async Task<IActionResult> Deactivate(string id)
        {
            if (!Ulid.TryParse(id, out var adminId)) return BadRequest("Invalid ID format");

            try
            {
                await _service.ToggleAdminStatusAsync(adminId, false);
                return NoContent();
            }
            catch (System.Collections.Generic.KeyNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
        }
    }
}
