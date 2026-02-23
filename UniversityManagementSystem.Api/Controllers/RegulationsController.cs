using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UniversityManagementSystem.Core.DTOs;
using UniversityManagementSystem.Core.Entities;
using UniversityManagementSystem.Core.Interfaces;

namespace UniversityManagementSystem.Api.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class RegulationsController(IRegulationService service, IDistributedCache cache) : ControllerBase
    {
        private readonly IRegulationService _service = service;
        private readonly IDistributedCache _cache = cache;
        private const string CacheKey = "Regulations_All";

        [HttpGet]
        public async Task<ActionResult<IEnumerable<RegulationDto>>> GetAll()
        {
            var cachedData = await _cache.GetStringAsync(CacheKey);
            if (!string.IsNullOrEmpty(cachedData))
            {
                return Ok(JsonSerializer.Deserialize<IEnumerable<RegulationDto>>(cachedData));
            }

            var list = await _service.GetAllAsync();
            var dtos = list.Select(r => new RegulationDto
            {
                Id = r.Id,
                Title = r.Title,
                Content = r.Content,
                Type = r.Type.ToString(),
                IsActive = r.IsActive
            });

            await _cache.SetStringAsync(CacheKey, JsonSerializer.Serialize(dtos), new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
            });

            return Ok(dtos);
        }

        [HttpGet("active")]
        public async Task<ActionResult<IEnumerable<RegulationDto>>> GetActive()
        {
            var list = await _service.GetActiveAsync();
            return Ok(list.Select(r => new RegulationDto
            {
                Id = r.Id,
                Title = r.Title,
                Content = r.Content,
                Type = r.Type.ToString(),
                IsActive = r.IsActive
            }));
        }

        [HttpPost]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<ActionResult<RegulationDto>> Create(CreateRegulationDto dto)
        {
            var entity = new Regulation
            {
                Title = dto.Title,
                Content = dto.Content,
                Type = dto.Type,
                IsActive = true
            };
            var result = await _service.CreateAsync(entity);

            // Invalidate cache
            await _cache.RemoveAsync(CacheKey);

            return Ok(new RegulationDto
            {
                Id = result.Id,
                Title = result.Title,
                Content = result.Content,
                Type = result.Type.ToString(),
                IsActive = result.IsActive
            });
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Update(int id, UpdateRegulationDto dto)
        {
            var entity = new Regulation
            {
                Title = dto.Title,
                Content = dto.Content,
                Type = dto.Type,
                IsActive = dto.IsActive
            };
            await _service.UpdateAsync(id, entity);
            return NoContent();
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int id)
        {
            await _service.DeleteAsync(id);
            return NoContent();
        }
    }
}
