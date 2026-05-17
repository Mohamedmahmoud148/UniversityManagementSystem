using System;
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
    [Authorize]
    public class ScheduleController(IScheduleService scheduleService) : ControllerBase
    {
        private readonly IScheduleService _scheduleService = scheduleService;

        // ── ADMIN — Create ────────────────────────────────────────────────────

        /// <summary>
        /// Admin creates a recurring weekly slot for a batch.
        /// POST /api/schedule
        /// </summary>
        [HttpPost]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<IActionResult> Create([FromBody] CreateScheduleEntryDto dto)
        {
            var result = await _scheduleService.CreateAsync(dto);
            return CreatedAtAction(nameof(GetByBatch), new { batchId = result.Id }, result);
        }

        // ── ADMIN — Update ────────────────────────────────────────────────────

        /// <summary>
        /// Admin updates an existing schedule slot.
        /// PUT /api/schedule/{id}
        /// </summary>
        [HttpPut("{id}")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<IActionResult> Update(string id, [FromBody] UpdateScheduleEntryDto dto)
        {
            if (!Ulid.TryParse(id, out var uid)) return BadRequest("Invalid Schedule Entry ID.");
            var result = await _scheduleService.UpdateAsync(uid, dto);
            return Ok(result);
        }

        // ── ADMIN — Delete ────────────────────────────────────────────────────

        /// <summary>
        /// Admin permanently removes a schedule slot.
        /// DELETE /api/schedule/{id}
        /// </summary>
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<IActionResult> Delete(string id)
        {
            if (!Ulid.TryParse(id, out var uid)) return BadRequest("Invalid Schedule Entry ID.");
            await _scheduleService.DeleteAsync(uid);
            return NoContent();
        }

        // ── READ — Full week for a batch ──────────────────────────────────────

        /// <summary>
        /// Returns the full weekly schedule for a batch (all days, sorted by day+time).
        /// GET /api/schedule/batch/{batchId}
        /// Accessible by: Admin, Doctor, Student
        /// </summary>
        [HttpGet("batch/{batchId}")]
        [Authorize(Roles = "Admin,Doctor,Student,SuperAdmin")]
        public async Task<IActionResult> GetByBatch(string batchId)
        {
            if (!Ulid.TryParse(batchId, out var uid)) return BadRequest("Invalid Batch ID.");
            var result = await _scheduleService.GetByBatchAsync(uid);
            return Ok(result);
        }

        // ── READ — Today's schedule for a batch ───────────────────────────────

        /// <summary>
        /// Returns today's schedule for a batch (server clock, UTC).
        /// Each slot has an IsNow flag showing if the class is happening right now.
        /// GET /api/schedule/batch/{batchId}/today
        /// Accessible by: Admin, Doctor, Student
        /// </summary>
        [HttpGet("batch/{batchId}/today")]
        [Authorize(Roles = "Admin,Doctor,Student,SuperAdmin")]
        public async Task<IActionResult> GetToday(string batchId)
        {
            if (!Ulid.TryParse(batchId, out var uid)) return BadRequest("Invalid Batch ID.");
            var result = await _scheduleService.GetTodayAsync(uid);
            return Ok(result);
        }

        // ── READ — Specific day for a batch ───────────────────────────────────

        /// <summary>
        /// Returns the schedule for a specific day of the week for a batch.
        /// day: 0=Sunday, 1=Monday, 2=Tuesday, 3=Wednesday, 4=Thursday, 5=Friday, 6=Saturday
        /// GET /api/schedule/batch/{batchId}/day/{day}
        /// Accessible by: Admin, Doctor, Student
        /// </summary>
        [HttpGet("batch/{batchId}/day/{day:int}")]
        [Authorize(Roles = "Admin,Doctor,Student,SuperAdmin")]
        public async Task<IActionResult> GetByDay(string batchId, int day)
        {
            if (!Ulid.TryParse(batchId, out var uid)) return BadRequest("Invalid Batch ID.");
            if (day < 0 || day > 6) return BadRequest("Day must be between 0 (Sunday) and 6 (Saturday).");
            var result = await _scheduleService.GetByDayAsync(uid, (DayOfWeek)day);
            return Ok(result);
        }

        // ── READ — By Subject Offering ─────────────────────────────────────────

        /// <summary>
        /// Returns all schedule slots for a specific subject offering.
        /// GET /api/schedule/offering/{offeringId}
        /// </summary>
        [HttpGet("offering/{offeringId}")]
        [Authorize(Roles = "Admin,Doctor,Student,SuperAdmin")]
        public async Task<IActionResult> GetByOffering(string offeringId)
        {
            if (!Ulid.TryParse(offeringId, out var uid)) return BadRequest("Invalid Offering ID.");
            var result = await _scheduleService.GetByOfferingAsync(uid);
            return Ok(result);
        }

        // ── READ — Doctor's full weekly schedule ──────────────────────────────

        /// <summary>
        /// Doctor views their own full weekly schedule (all batches they teach).
        /// GET /api/schedule/my-schedule
        /// Accessible by: Doctor only
        /// </summary>
        [HttpGet("my-schedule")]
        [Authorize(Roles = "Doctor,SuperAdmin")]
        public async Task<IActionResult> GetMySchedule()
        {
            var profileClaim = User.FindFirst("ProfileId");
            if (profileClaim == null || !Ulid.TryParse(profileClaim.Value, out var doctorId))
                return Unauthorized("Doctor profile not found in token.");

            var result = await _scheduleService.GetDoctorScheduleAsync(doctorId);
            return Ok(result);
        }

        // ── READ — Doctor's today schedule ────────────────────────────────────

        /// <summary>
        /// Doctor views today's classes they're teaching.
        /// GET /api/schedule/my-today
        /// Accessible by: Doctor only
        /// </summary>
        [HttpGet("my-today")]
        [Authorize(Roles = "Doctor,SuperAdmin")]
        public async Task<IActionResult> GetMyToday()
        {
            var profileClaim = User.FindFirst("ProfileId");
            if (profileClaim == null || !Ulid.TryParse(profileClaim.Value, out var doctorId))
                return Unauthorized("Doctor profile not found in token.");

            var result = await _scheduleService.GetDoctorTodayAsync(doctorId);
            return Ok(result);
        }
    }
}
