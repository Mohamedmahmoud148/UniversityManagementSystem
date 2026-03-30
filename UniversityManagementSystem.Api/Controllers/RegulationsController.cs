using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using NUlid;
using UniversityManagementSystem.Core.DTOs;
using UniversityManagementSystem.Core.Entities;
using UniversityManagementSystem.Core.Interfaces;
using UniversityManagementSystem.Infrastructure.Data;

namespace UniversityManagementSystem.Api.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class RegulationsController(
        IRegulationService service,
        IDistributedCache cache,
        AppDbContext context,
        IStorageService storageService,
        IFileService fileService) : ControllerBase
    {
        private readonly IRegulationService _service = service;
        private readonly IDistributedCache _cache = cache;
        private readonly AppDbContext _context = context;
        private readonly IStorageService _storageService = storageService;
        private readonly IFileService _fileService = fileService;
        private const string CacheKey = "Regulations_All";

        // Allowed file MIME types for regulation attachments
        private static readonly HashSet<string> AllowedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "application/pdf",
            "application/msword",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            "application/vnd.ms-excel",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "text/plain"
        };
        private const long MaxFileSizeBytes = 50 * 1024 * 1024; // 50 MB

        // ── Helper: build RegulationDto with signed URL ───────────────────────
        private async Task<RegulationDto> ToDto(Regulation r)
        {
            string? fileUrl = null;
            if (r.FileId.HasValue)
            {
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
            var cached = await _cache.GetStringAsync(CacheKey);
            if (!string.IsNullOrEmpty(cached))
                return Ok(JsonSerializer.Deserialize<IEnumerable<RegulationDto>>(cached));

            var list = await _service.GetAllAsync();
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

        // ── POST /api/Regulations ─────────────────────────────────────────────
        /// <summary>
        /// Create a regulation. Supports two modes:
        /// 1) Text-only:          form fields: title, content, type
        /// 2) File attachment:    form fields: title, type + file (PDF/Word/Excel/TXT)
        ///    Content and File can both be provided simultaneously.
        /// </summary>
        [HttpPost]
        [Authorize(Roles = "Admin,SuperAdmin")]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(52_428_800)] // 50 MB
        public async Task<ActionResult<RegulationDto>> Create([FromForm] CreateRegulationDto dto)
        {
            // Validation: need at least content OR a file
            if (string.IsNullOrWhiteSpace(dto.Content) && dto.File == null)
                return BadRequest("At least one of 'content' or a file attachment must be provided.");

            // Validate file if present
            if (dto.File != null)
            {
                if (!AllowedMimeTypes.Contains(dto.File.ContentType))
                    return BadRequest($"File type '{dto.File.ContentType}' is not allowed. Supported: PDF, DOC, DOCX, XLS, XLSX, TXT.");

                if (dto.File.Length > MaxFileSizeBytes)
                    return BadRequest("File exceeds the 50 MB size limit.");
            }

            // Resolve current user (Admin/SuperAdmin) for file upload
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            Ulid? uploadedFileId = null;

            if (dto.File != null)
            {
                if (!Ulid.TryParse(userIdStr, out var uploaderId))
                    return Unauthorized("Cannot resolve current user from token.");

                // Reuse existing FileService — stream upload to R2 "files/" folder
                var uploaded = await _fileService.UploadFileStreamAsync(
                    userId: uploaderId,
                    stream: dto.File.OpenReadStream(),
                    fileName: dto.File.FileName,
                    contentType: dto.File.ContentType,
                    fileLength: dto.File.Length);

                uploadedFileId = uploaded.Id;
            }

            var entity = new Regulation
            {
                Title = dto.Title,
                Content = string.IsNullOrWhiteSpace(dto.Content) ? null : dto.Content,
                Type = dto.Type,
                FileId = uploadedFileId,
                IsActive = true
            };

            var result = await _service.CreateAsync(entity);
            await _cache.RemoveAsync(CacheKey);

            return Ok(await ToDto(result));
        }

        // ── PUT /api/Regulations/{id} ─────────────────────────────────────────
        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(52_428_800)]
        public async Task<IActionResult> Update(string id, [FromForm] UpdateRegulationDto dto)
        {
            if (!Ulid.TryParse(id, out var regId))
                return BadRequest("Invalid Regulation ID.");

            if (string.IsNullOrWhiteSpace(dto.Content) && dto.File == null)
                return BadRequest("At least one of 'content' or a file attachment must be provided.");

            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            Ulid? uploadedFileId = null;

            if (dto.File != null)
            {
                if (dto.File.Length > MaxFileSizeBytes)
                    return BadRequest("File exceeds the 50 MB size limit.");

                if (!AllowedMimeTypes.Contains(dto.File.ContentType))
                    return BadRequest($"File type '{dto.File.ContentType}' is not allowed.");

                if (!Ulid.TryParse(userIdStr, out var uploaderId))
                    return Unauthorized("Cannot resolve current user from token.");

                var uploaded = await _fileService.UploadFileStreamAsync(
                    userId: uploaderId,
                    stream: dto.File.OpenReadStream(),
                    fileName: dto.File.FileName,
                    contentType: dto.File.ContentType,
                    fileLength: dto.File.Length);

                uploadedFileId = uploaded.Id;
            }

            var entity = new Regulation
            {
                Title = dto.Title,
                Content = string.IsNullOrWhiteSpace(dto.Content) ? null : dto.Content,
                Type = dto.Type,
                FileId = uploadedFileId,
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
            if (!Ulid.TryParse(id, out var regId))
                return BadRequest("Invalid Regulation ID.");

            await _service.DeleteAsync(regId);
            await _cache.RemoveAsync(CacheKey);
            return NoContent();
        }
    }
}
