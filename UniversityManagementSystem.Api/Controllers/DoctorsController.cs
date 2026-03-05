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
using NUlid;

namespace UniversityManagementSystem.Api.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class DoctorsController(IDoctorService service, IAuthService authService) : ControllerBase
    {
        private readonly IDoctorService _service = service;
        private readonly IAuthService _authService = authService;

        [HttpGet]
        public async Task<ActionResult<IEnumerable<DoctorDto>>> GetAll([FromQuery] int page = 1, [FromQuery] int size = 10)
        {
            var list = await _service.GetPagedDoctorsAsync(page, size);
            return Ok(list.Select(d => new DoctorDto
            {
                Id = d.Id,
                Code = d.Code,
                FullName = d.FullName,
                Email = d.Email,
                Phone = d.Phone,
                UniversityStaffId = d.UniversityStaffId,
                UniversityEmail = d.SystemUser?.UniversityEmail ?? "N/A",
                DepartmentId = d.DepartmentId
            }));
        }

        [HttpGet("by-code/{code}")]
        public async Task<ActionResult<DoctorDto>> GetByCode(string code)
        {
            var d = await _service.GetDoctorByCodeAsync(code);
            if (d == null) return NotFound();

            return Ok(new DoctorDto
            {
                Id = d.Id,
                Code = d.Code,
                FullName = d.FullName,
                Email = d.Email,
                Phone = d.Phone,
                UniversityStaffId = d.UniversityStaffId,
                UniversityEmail = d.SystemUser?.UniversityEmail ?? "N/A",
                DepartmentId = d.DepartmentId
            });
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<DoctorDto>> Create(CreateDoctorDto dto)
        {
            var registerDto = new RegisterDoctorDto
            {
                FullName = dto.FullName,
                Phone = dto.Phone,
                NationalId = dto.NationalId,
                DepartmentId = dto.DepartmentId
            };

            var creatorId = Ulid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
            var authResponse = await _authService.RegisterDoctorAsync(registerDto, creatorId);

            var doctor = await _service.GetDoctorByUniversityEmailAsync(authResponse.UniversityEmail!);
            if (doctor == null) return BadRequest("Failed");

            return Ok(new DoctorDto
            {
                Id = doctor.Id,
                Code = doctor.Code,
                FullName = doctor.FullName,
                Email = doctor.Email,
                Phone = doctor.Phone,
                UniversityStaffId = doctor.UniversityStaffId,
                UniversityEmail = doctor.SystemUser?.UniversityEmail ?? "N/A",
                DepartmentId = doctor.DepartmentId
            });
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Update(Ulid id, UpdateDoctorDto dto)
        {
            try
            {
                await _service.UpdateDoctorDetailsAsync(id, dto);
                return NoContent();
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(Ulid id)
        {
            try
            {
                await _service.DeleteDoctorAsync(id);
                return NoContent();
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpGet("{id}/subjects")]
        public async Task<ActionResult<IEnumerable<SubjectDto>>> GetSubjects(Ulid id)
        {
            var list = await _service.GetDoctorSubjectsAsync(id);
            return Ok(list.Select(s => new SubjectDto(s.Id, s.Name, s.Code, s.CollegeId, s.DepartmentId, s.BatchId)));
        }

        [HttpPost("bulk-upload")]
        [Authorize(Roles = "Admin")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> BulkUpload(IFormFile file, [FromServices] IFileService fileService, [FromServices] IBackgroundJobClient jobClient)
        {
            try
            {
                if (file == null || file.Length == 0) return BadRequest("File is empty");

                var userIdClaim = User.FindFirst("nameid");
                if (userIdClaim == null) return Unauthorized("Invalid token claims");

                var userId = Ulid.Parse(userIdClaim.Value);
                using var stream = file.OpenReadStream();
                var status = await fileService.UploadFileStreamAsync(userId, stream, file.FileName, file.ContentType, file.Length);

                jobClient.Enqueue<IBulkUploadJob>(x => x.ProcessDoctorUpload(status.Id, userId));
                return Accepted(new { JobId = status.Id, Message = "File accepted for processing" });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Doctors BulkUpload Error: {ex.Message}");
                return StatusCode(500, "An unexpected error occurred during bulk upload.");
            }
        }
    }
}
