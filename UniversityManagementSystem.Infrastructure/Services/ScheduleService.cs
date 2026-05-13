using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using NUlid;
using UniversityManagementSystem.Core.DTOs;
using UniversityManagementSystem.Core.Entities;
using UniversityManagementSystem.Core.Interfaces;
using UniversityManagementSystem.Infrastructure.Data;

namespace UniversityManagementSystem.Infrastructure.Services
{
    public class ScheduleService(AppDbContext context) : IScheduleService
    {
        private readonly AppDbContext _context = context;

        // ─────────────────────────────────────────────────────────────────────
        //  Admin CRUD
        // ─────────────────────────────────────────────────────────────────────

        public async Task<ScheduleEntryDto> CreateAsync(CreateScheduleEntryDto dto)
        {
            if (!Ulid.TryParse(dto.SubjectOfferingId, out var offeringId))
                throw new ArgumentException("Invalid SubjectOfferingId.");

            // Resolve the SubjectOffering to get BatchId (denormalise it on the entry)
            var offering = await _context.SubjectOfferings
                .AsNoTracking()
                .FirstOrDefaultAsync(o => o.Id == offeringId)
                ?? throw new KeyNotFoundException($"SubjectOffering '{dto.SubjectOfferingId}' not found.");

            Ulid? groupId = null;
            if (!string.IsNullOrWhiteSpace(dto.GroupId))
            {
                if (!Ulid.TryParse(dto.GroupId, out var gId))
                    throw new ArgumentException("Invalid GroupId.");
                groupId = gId;
            }

            var entry = new ScheduleEntry
            {
                SubjectOfferingId = offeringId,
                BatchId           = offering.BatchId,
                GroupId           = groupId,
                DayOfWeek         = dto.DayOfWeek,
                StartTime         = ParseTime(dto.StartTime),
                EndTime           = ParseTime(dto.EndTime),
                Type              = dto.Type,
                Location          = dto.Location.Trim(),
                WeekType          = dto.WeekType,
                IsActive          = true,
                CreatedAt         = DateTime.UtcNow
            };

            _context.ScheduleEntries.Add(entry);
            await _context.SaveChangesAsync();

            return await EnrichAsync(entry);
        }

        public async Task<ScheduleEntryDto> UpdateAsync(Ulid id, UpdateScheduleEntryDto dto)
        {
            var entry = await _context.ScheduleEntries.FindAsync(id)
                ?? throw new KeyNotFoundException($"ScheduleEntry '{id}' not found.");

            if (dto.DayOfWeek.HasValue)  entry.DayOfWeek = dto.DayOfWeek.Value;
            if (dto.StartTime != null)   entry.StartTime  = ParseTime(dto.StartTime);
            if (dto.EndTime   != null)   entry.EndTime    = ParseTime(dto.EndTime);
            if (dto.Type.HasValue)       entry.Type       = dto.Type.Value;
            if (dto.Location  != null)   entry.Location   = dto.Location.Trim();
            if (dto.WeekType.HasValue)   entry.WeekType   = dto.WeekType.Value;
            if (dto.IsActive.HasValue)   entry.IsActive   = dto.IsActive.Value;

            await _context.SaveChangesAsync();
            return await EnrichAsync(entry);
        }

        public async Task DeleteAsync(Ulid id)
        {
            var entry = await _context.ScheduleEntries.FindAsync(id)
                ?? throw new KeyNotFoundException($"ScheduleEntry '{id}' not found.");

            _context.ScheduleEntries.Remove(entry);
            await _context.SaveChangesAsync();
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Read — Batch
        // ─────────────────────────────────────────────────────────────────────

        public async Task<IEnumerable<ScheduleEntryDto>> GetByBatchAsync(Ulid batchId)
        {
            var entries = await _context.ScheduleEntries
                .AsNoTracking()
                .Include(e => e.SubjectOffering).ThenInclude(o => o.Subject)
                .Include(e => e.SubjectOffering).ThenInclude(o => o.Doctor)
                .Include(e => e.Batch)
                .Include(e => e.Group)
                .Where(e => e.BatchId == batchId && e.IsActive)
                .OrderBy(e => e.DayOfWeek)
                .ThenBy(e => e.StartTime)
                .ToListAsync();

            return entries.Select(ToDto);
        }

        public async Task<IEnumerable<TodayScheduleDto>> GetTodayAsync(Ulid batchId)
        {
            var today = DateTime.UtcNow.DayOfWeek;
            return await GetByDayAsync(batchId, today);
        }

        public async Task<IEnumerable<TodayScheduleDto>> GetByDayAsync(Ulid batchId, DayOfWeek day)
        {
            var now = DateTime.UtcNow.TimeOfDay;

            var entries = await _context.ScheduleEntries
                .AsNoTracking()
                .Include(e => e.SubjectOffering).ThenInclude(o => o.Subject)
                .Include(e => e.SubjectOffering).ThenInclude(o => o.Doctor)
                .Include(e => e.Group)
                .Where(e => e.BatchId == batchId && e.IsActive && e.DayOfWeek == day)
                .OrderBy(e => e.StartTime)
                .ToListAsync();

            return entries.Select(e => ToTodayDto(e, now));
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Read — Doctor
        // ─────────────────────────────────────────────────────────────────────

        public async Task<IEnumerable<ScheduleEntryDto>> GetDoctorScheduleAsync(Ulid doctorId)
        {
            var entries = await _context.ScheduleEntries
                .AsNoTracking()
                .Include(e => e.SubjectOffering).ThenInclude(o => o.Subject)
                .Include(e => e.SubjectOffering).ThenInclude(o => o.Doctor)
                .Include(e => e.Batch)
                .Include(e => e.Group)
                .Where(e => e.SubjectOffering.DoctorId == doctorId && e.IsActive)
                .OrderBy(e => e.DayOfWeek)
                .ThenBy(e => e.StartTime)
                .ToListAsync();

            return entries.Select(ToDto);
        }

        public async Task<IEnumerable<TodayScheduleDto>> GetDoctorTodayAsync(Ulid doctorId)
        {
            var today = DateTime.UtcNow.DayOfWeek;
            var now   = DateTime.UtcNow.TimeOfDay;

            var entries = await _context.ScheduleEntries
                .AsNoTracking()
                .Include(e => e.SubjectOffering).ThenInclude(o => o.Subject)
                .Include(e => e.SubjectOffering).ThenInclude(o => o.Doctor)
                .Include(e => e.Batch)
                .Include(e => e.Group)
                .Where(e => e.SubjectOffering.DoctorId == doctorId
                         && e.IsActive
                         && e.DayOfWeek == today)
                .OrderBy(e => e.StartTime)
                .ToListAsync();

            return entries.Select(e => ToTodayDto(e, now));
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Read — By Offering
        // ─────────────────────────────────────────────────────────────────────

        public async Task<IEnumerable<ScheduleEntryDto>> GetByOfferingAsync(Ulid offeringId)
        {
            var entries = await _context.ScheduleEntries
                .AsNoTracking()
                .Include(e => e.SubjectOffering).ThenInclude(o => o.Subject)
                .Include(e => e.SubjectOffering).ThenInclude(o => o.Doctor)
                .Include(e => e.Batch)
                .Include(e => e.Group)
                .Where(e => e.SubjectOfferingId == offeringId && e.IsActive)
                .OrderBy(e => e.DayOfWeek)
                .ThenBy(e => e.StartTime)
                .ToListAsync();

            return entries.Select(ToDto);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Private helpers
        // ─────────────────────────────────────────────────────────────────────

        private static TimeSpan ParseTime(string t)
        {
            if (TimeSpan.TryParseExact(t, @"hh\:mm", null, out var ts)) return ts;
            if (TimeSpan.TryParseExact(t, @"h\:mm",  null, out ts))     return ts;
            throw new ArgumentException($"Invalid time format '{t}'. Expected HH:mm.");
        }

        private static string FormatTime(TimeSpan t) => t.ToString(@"hh\:mm");

        private static ScheduleEntryDto ToDto(ScheduleEntry e) => new()
        {
            Id                = e.Id.ToString(),
            SubjectOfferingId = e.SubjectOfferingId.ToString(),
            SubjectCode       = e.SubjectOffering?.Subject?.Code   ?? string.Empty,
            SubjectName       = e.SubjectOffering?.Subject?.Name   ?? string.Empty,
            DoctorName        = e.SubjectOffering?.Doctor?.FullName ?? string.Empty,
            BatchName         = e.Batch?.Name   ?? string.Empty,
            GroupName         = e.Group?.Name,
            DayOfWeek         = e.DayOfWeek.ToString(),
            DayOfWeekNumber   = (int)e.DayOfWeek,
            StartTime         = FormatTime(e.StartTime),
            EndTime           = FormatTime(e.EndTime),
            Type              = e.Type.ToString(),
            Location          = e.Location,
            WeekType          = e.WeekType.ToString(),
            IsActive          = e.IsActive
        };

        private static TodayScheduleDto ToTodayDto(ScheduleEntry e, TimeSpan now) => new()
        {
            Id          = e.Id.ToString(),
            SubjectName = e.SubjectOffering?.Subject?.Name    ?? string.Empty,
            SubjectCode = e.SubjectOffering?.Subject?.Code    ?? string.Empty,
            DoctorName  = e.SubjectOffering?.Doctor?.FullName ?? string.Empty,
            StartTime   = FormatTime(e.StartTime),
            EndTime     = FormatTime(e.EndTime),
            Type        = e.Type.ToString(),
            Location    = e.Location,
            GroupName   = e.Group?.Name,
            IsNow       = now >= e.StartTime && now <= e.EndTime
        };

        /// <summary>Re-loads navigation properties after insert/update for a proper DTO.</summary>
        private async Task<ScheduleEntryDto> EnrichAsync(ScheduleEntry entry)
        {
            await _context.Entry(entry)
                .Reference(e => e.SubjectOffering).LoadAsync();
            if (entry.SubjectOffering != null)
            {
                await _context.Entry(entry.SubjectOffering)
                    .Reference(o => o.Subject).LoadAsync();
                await _context.Entry(entry.SubjectOffering)
                    .Reference(o => o.Doctor).LoadAsync();
            }
            await _context.Entry(entry).Reference(e => e.Batch).LoadAsync();
            if (entry.GroupId.HasValue)
                await _context.Entry(entry).Reference(e => e.Group).LoadAsync();

            return ToDto(entry);
        }
    }
}
