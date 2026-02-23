using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UniversityManagementSystem.Core.DTOs;
using UniversityManagementSystem.Core.Entities;
using UniversityManagementSystem.Core.Interfaces;
using Hangfire;
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;
using System;
using Microsoft.AspNetCore.Http;

namespace UniversityManagementSystem.Api.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class SubjectsController(ISubjectService service, IDistributedCache cache) : ControllerBase
    {
        private readonly ISubjectService _service = service;
        private readonly IDistributedCache _cache = cache;
        private const string CachePrefix = "Subjects_Batch_";

        [HttpGet("by-batch/{batchId}")]
        public async Task<ActionResult<IEnumerable<SubjectDto>>> GetByBatch(int batchId)
        {
            var cacheKey = $"{CachePrefix}{batchId}";
            var cachedData = await _cache.GetStringAsync(cacheKey);
            if (!string.IsNullOrEmpty(cachedData))
            {
                return Ok(JsonSerializer.Deserialize<IEnumerable<SubjectDto>>(cachedData));
            }

            var list = await _service.GetSubjectsByBatchIdAsync(batchId);
            var dtos = list.Select(s => new SubjectDto(s.Id, s.Name, s.Code, s.CollegeId, s.DepartmentId, s.BatchId));

            await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(dtos), new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
            });

            return Ok(dtos);
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<SubjectDto>> Create(CreateSubjectDto dto)
        {
            var entity = new Subject
            {
                Name = dto.Name,
                Code = dto.Code,
                CollegeId = dto.CollegeId,
                DepartmentId = dto.DepartmentId,
                BatchId = dto.BatchId
            };
            var result = await _service.CreateSubjectAsync(entity);

            // Invalidate relevant batch cache
            await _cache.RemoveAsync($"{CachePrefix}{dto.BatchId}");

            return Ok(new SubjectDto(result.Id, result.Name, result.Code, result.CollegeId, result.DepartmentId, result.BatchId));
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Update(int id, UpdateSubjectDto dto)
        {
            try
            {
                await _service.UpdateSubjectDetailsAsync(id, dto);
                return NoContent(); // Cache will expire naturally
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                await _service.DeleteSubjectAsync(id);
                return NoContent();
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPut("assign-doctor")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> AssignDoctor(int subjectId, int doctorId)
        {
            await _service.AssignSubjectToDoctorAsync(subjectId, doctorId);
            return NoContent();
        }

        [HttpPut("assign-assistant")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> AssignAssistant(int subjectId, int assistantId)
        {
            await _service.AssignSubjectToAssistantAsync(subjectId, assistantId);
            return NoContent();
        }
    }
}
