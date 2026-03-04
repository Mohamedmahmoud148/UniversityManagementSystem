using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UniversityManagementSystem.Core.DTOs;
using UniversityManagementSystem.Core.Entities;
using UniversityManagementSystem.Core.Interfaces;

namespace UniversityManagementSystem.Api.Controllers
{
    [Authorize(Roles = "Admin,Student,Doctor")]
    [ApiController]
    [Route("api/[controller]")]
    public class UniversityController : ControllerBase
    {
        private readonly IUniversityService _service;
        public UniversityController(IUniversityService service) => _service = service;

        [HttpGet("structure")]
        public async Task<ActionResult<IEnumerable<UniversityDto>>> GetStructure()
        {
            var universities = await _service.GetAllUniversitiesAsync();
            return Ok(universities.Select(u => new UniversityDto(u.Id, u.Name)));
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<UniversityDto>> Create(CreateUniversityDto dto)
        {
            var entity = new University { Name = dto.Name };
            var result = await _service.CreateUniversityAsync(entity);
            return Ok(new UniversityDto(result.Id, result.Name));
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Update(int id, CreateUniversityDto dto)
        {
            var entity = await _service.GetUniversityByIdAsync(id);
            if (entity == null) return NotFound();
            entity.Name = dto.Name;
            await _service.UpdateUniversityAsync(entity);
            return NoContent();
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int id)
        {
            await _service.DeleteUniversityAsync(id);
            return NoContent();
        }
    }

    [Authorize(Roles = "Admin,Student,Doctor")]
    [ApiController]
    [Route("api/[controller]")]
    public class CollegesController(ICollegeService service) : ControllerBase
    {
        private readonly ICollegeService _service = service;

        [HttpGet]
        public async Task<ActionResult<IEnumerable<CollegeDto>>> GetAll()
        {
            var list = await _service.GetAllCollegesAsync();
            return Ok(list.Select(c => new CollegeDto(c.Id, c.PublicId, c.Name, c.UniversityId)));
        }

        [HttpGet("by-public-id/{publicId}")]
        public async Task<ActionResult<CollegeDto>> GetByPublicId(string publicId)
        {
            var c = await _service.GetCollegeByPublicIdAsync(publicId);
            if (c == null) return NotFound();

            return Ok(new CollegeDto(c.Id, c.PublicId, c.Name, c.UniversityId));
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<CollegeDto>> Create(CreateCollegeDto dto)
        {
            var entity = new College { Name = dto.Name, UniversityId = dto.UniversityId };
            var result = await _service.CreateCollegeAsync(entity);
            return Ok(new CollegeDto(result.Id, result.PublicId, result.Name, result.UniversityId));
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Update(int id, CreateCollegeDto dto)
        {
            var entity = await _service.GetCollegeByIdAsync(id);
            if (entity == null) return NotFound();
            entity.Name = dto.Name;
            entity.UniversityId = dto.UniversityId;
            await _service.UpdateCollegeAsync(entity);
            return NoContent();
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int id)
        {
            await _service.DeleteCollegeAsync(id);
            return NoContent();
        }
    }

    [Authorize(Roles = "Admin,Student,Doctor")]
    [ApiController]
    [Route("api/[controller]")]
    public class DepartmentsController(IDepartmentService service) : ControllerBase
    {
        private readonly IDepartmentService _service = service;

        [HttpGet("by-college/{collegeId}")]
        public async Task<ActionResult<IEnumerable<DepartmentDto>>> GetByCollege(int collegeId)
        {
            var list = await _service.GetDepartmentsByCollegeIdAsync(collegeId);
            return Ok(list.Select(d => new DepartmentDto(d.Id, d.PublicId, d.Name, d.CollegeId)));
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<DepartmentDto>> Create(CreateDepartmentDto dto)
        {
            var entity = new Department { Name = dto.Name, CollegeId = dto.CollegeId };
            var result = await _service.CreateDepartmentAsync(entity);
            return Ok(new DepartmentDto(result.Id, result.PublicId, result.Name, result.CollegeId));
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Update(int id, CreateDepartmentDto dto)
        {
            var entity = await _service.GetDepartmentByIdAsync(id);
            if (entity == null) return NotFound();
            entity.Name = dto.Name;
            entity.CollegeId = dto.CollegeId;
            await _service.UpdateDepartmentAsync(entity);
            return NoContent();
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int id)
        {
            await _service.DeleteDepartmentAsync(id);
            return NoContent();
        }
    }

    [Authorize(Roles = "Admin,Student,Doctor")]
    [ApiController]
    [Route("api/[controller]")]
    public class BatchesController(IBatchService service) : ControllerBase
    {
        private readonly IBatchService _service = service;

        [HttpGet("by-department/{departmentId}")]
        public async Task<ActionResult<IEnumerable<BatchDto>>> GetByDepartment(int departmentId)
        {
            var list = await _service.GetBatchesByDepartmentIdAsync(departmentId);
            return Ok(list.Select(b => new BatchDto(b.Id, b.Name, b.DepartmentId)));
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<BatchDto>> Create(CreateBatchDto dto)
        {
            var entity = new Batch { Name = dto.Name, DepartmentId = dto.DepartmentId };
            var result = await _service.CreateBatchAsync(entity);
            return Ok(new BatchDto(result.Id, result.Name, result.DepartmentId));
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Update(int id, CreateBatchDto dto)
        {
            var entity = await _service.GetBatchByIdAsync(id);
            if (entity == null) return NotFound();
            entity.Name = dto.Name;
            entity.DepartmentId = dto.DepartmentId;
            await _service.UpdateBatchAsync(entity);
            return NoContent();
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int id)
        {
            await _service.DeleteBatchAsync(id);
            return NoContent();
        }
    }

    [Authorize(Roles = "Admin,Student,Doctor")]
    [ApiController]
    [Route("api/[controller]")]
    public class GroupsController(IGroupService service) : ControllerBase
    {
        private readonly IGroupService _service = service;

        [HttpGet("by-batch/{batchId}")]
        public async Task<ActionResult<IEnumerable<GroupDto>>> GetByBatch(int batchId)
        {
            var list = await _service.GetGroupsByBatchIdAsync(batchId);
            return Ok(list.Select(g => new GroupDto(g.Id, g.Name, g.BatchId)));
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<GroupDto>> Create(CreateGroupDto dto)
        {
            var entity = new Group { Name = dto.Name, BatchId = dto.BatchId };
            var result = await _service.CreateGroupAsync(entity);
            return Ok(new GroupDto(result.Id, result.Name, result.BatchId));
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Update(int id, CreateGroupDto dto)
        {
            var entity = await _service.GetGroupByIdAsync(id);
            if (entity == null) return NotFound();
            entity.Name = dto.Name;
            entity.BatchId = dto.BatchId;
            await _service.UpdateGroupAsync(entity);
            return NoContent();
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int id)
        {
            await _service.DeleteGroupAsync(id);
            return NoContent();
        }
    }
}
