using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using UniversityManagementSystem.Core.DTOs;
using UniversityManagementSystem.Core.Entities;
using UniversityManagementSystem.Core.Interfaces;
using UniversityManagementSystem.Infrastructure.Data;
using NUlid;

namespace UniversityManagementSystem.Api.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class RegulationsController(
        IRegulationService service,
        IDistributedCache cache,
        AppDbContext context,
        IStorageService storageService) : ControllerBase
    {
        private readonly IRegulationService _service = service;
        private readonly IDistributedCache _cache = cache;
        private readonly AppDbContext _context = context;
        private readonly IStorageService _storageService = storageService;
        private const string CacheKey = "Regulations_All";

        // ── Helper: build a RegulationDto from a Regulation entity (with optional signed URL) ──
        private async Task<RegulationDto> ToDto(Regulation r)
        {
            string? fileUrl = null;
            if (r.FileId.HasValue)
            {
                // Resolve the storage key then generate a signed URL
                var file = await _context.UploadedFiles.FindAsync(r.FileId.Value);
                if (file != null)
                {
                    var key = !string.IsNullOrWhiteSpace(file.StorageKey) ? file.StorageKey : file.StoredPath;
                    fileUrl = await _storageService.GenerateSignedUrlAsync(key, expiryMinutes: 60);
                }
            }

            return new RegulationDto
            {
                Id = r.Id,
                Title = r.Title,
                Content = r.Content,
                Type = r.Type.ToString(),
                IsActive = r.IsActive,
                FileId = r.FileId?.ToString(),
                FileUrl = fileUrl
            };
        }

        // ── GET /api/Regulations ─────────────────────────────────────────────
        [HttpGet]
        public async Task<ActionResult<IEnumerable<RegulationDto>>> GetAll()
        {
            var cachedData = await _cache.GetStringAsync(CacheKey);
            if (!string.IsNullOrEmpty(cachedData))
                return Ok(JsonSerializer.Deserialize<IEnumerable<RegulationDto>>(cachedData));

            var list = await _service.GetAllAsync();

            // Build DTOs with signed URLs for any file-linked regulations
            var dtos = new List<RegulationDto>();
            foreach (var r in list)
                dtos.Add(await ToDto(r));

            await _cache.SetStringAsync(CacheKey, JsonSerializer.Serialize(dtos),
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5) });

            return Ok(dtos);
        }

        // ── GET /api/Regulations/active ──────────────────────────────────────
        [HttpGet("active")]
        public async Task<ActionResult<IEnumerable<RegulationDto>>> GetActive()
        {
            var list = await _service.GetActiveAsync();
            var dtos = new List<RegulationDto>();
            foreach (var r in list)
                dtos.Add(await ToDto(r));
            return Ok(dtos);
        }

        // ── POST /api/Regulations ────────────────────────────────────────────
        [HttpPost]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<ActionResult<RegulationDto>> Create([FromBody] CreateRegulationDto dto)
        {
            // Validation: at least one of Content or FileId is required
            if (string.IsNullOrWhiteSpace(dto.Content) && string.IsNullOrWhiteSpace(dto.FileId))
                return BadRequest("At least one of 'content' or 'fileId' must be provided.");

            Ulid? fileId = null;
            if (!string.IsNullOrWhiteSpace(dto.FileId))
            {
                if (!Ulid.TryParse(dto.FileId, out var parsedFileId))
                    return BadRequest("Invalid FileId format.");

                // Verify the file exists in DB
                var fileExists = await _context.UploadedFiles.AnyAsync(f => f.Id == parsedFileId);
                if (!fileExists)
                    return NotFound($"File with ID '{dto.FileId}' not found. Upload the file first via POST /api/File/upload.");

                fileId = parsedFileId;
            }

            var entity = new Regulation
            {
                Title = dto.Title,
                Content = string.IsNullOrWhiteSpace(dto.Content) ? null : dto.Content,
                Type = dto.Type,
                FileId = fileId,
                IsActive = true
            };

            var result = await _service.CreateAsync(entity);

            // Invalidate cache
            await _cache.RemoveAsync(CacheKey);

            return Ok(await ToDto(result));
        }

        // ── PUT /api/Regulations/{id} ────────────────────────────────────────
        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Update(string id, [FromBody] UpdateRegulationDto dto)
        {
            if (!Ulid.TryParse(id, out var regId)) return BadRequest("Invalid Regulation ID.");

            if (string.IsNullOrWhiteSpace(dto.Content) && string.IsNullOrWhiteSpace(dto.FileId))
                return BadRequest("At least one of 'content' or 'fileId' must be provided.");

            Ulid? fileId = null;
            if (!string.IsNullOrWhiteSpace(dto.FileId))
            {
                if (!Ulid.TryParse(dto.FileId, out var parsedFileId))
                    return BadRequest("Invalid FileId format.");
                fileId = parsedFileId;
            }

            var entity = new Regulation
            {
                Title = dto.Title,
                Content = string.IsNullOrWhiteSpace(dto.Content) ? null : dto.Content,
                Type = dto.Type,
                FileId = fileId,
                IsActive = dto.IsActive
            };

            await _service.UpdateAsync(regId, entity);
            await _cache.RemoveAsync(CacheKey);
            return NoContent();
        }

        // ── DELETE /api/Regulations/{id} ─────────────────────────────────────
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(string id)
        {
            if (!Ulid.TryParse(id, out var regId)) return BadRequest("Invalid Regulation ID.");
            await _service.DeleteAsync(regId);
            await _cache.RemoveAsync(CacheKey);
            return NoContent();
        }
    }
}
