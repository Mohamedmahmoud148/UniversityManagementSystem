using System.Collections.Generic;
using System.Threading.Tasks;
using NUlid;
using UniversityManagementSystem.Core.DTOs;

namespace UniversityManagementSystem.Core.Interfaces
{
    public interface IScheduleService
    {
        // ── Admin CRUD ────────────────────────────────────────────────────────
        Task<ScheduleEntryDto> CreateAsync(CreateScheduleEntryDto dto);
        Task<ScheduleEntryDto> UpdateAsync(Ulid id, UpdateScheduleEntryDto dto);
        Task DeleteAsync(Ulid id);

        // ── Read: by batch (full weekly schedule) ────────────────────────────
        Task<IEnumerable<ScheduleEntryDto>> GetByBatchAsync(Ulid batchId);

        // ── Read: by offering (all slots of one subject) ─────────────────────
        Task<IEnumerable<ScheduleEntryDto>> GetByOfferingAsync(Ulid offeringId);

        // ── Read: today's schedule for a batch ───────────────────────────────
        Task<IEnumerable<TodayScheduleDto>> GetTodayAsync(Ulid batchId);

        // ── Read: a specific day for a batch ─────────────────────────────────
        Task<IEnumerable<TodayScheduleDto>> GetByDayAsync(Ulid batchId, System.DayOfWeek day);

        // ── Read: doctor's full weekly schedule ──────────────────────────────
        Task<IEnumerable<ScheduleEntryDto>> GetDoctorScheduleAsync(Ulid doctorId);

        // ── Read: doctor's today schedule ────────────────────────────────────
        Task<IEnumerable<TodayScheduleDto>> GetDoctorTodayAsync(Ulid doctorId);
    }
}
