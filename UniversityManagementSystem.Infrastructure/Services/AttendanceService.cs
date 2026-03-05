using Microsoft.EntityFrameworkCore;
using UniversityManagementSystem.Core.DTOs;
using UniversityManagementSystem.Core.Entities;
using UniversityManagementSystem.Core.Interfaces;
using UniversityManagementSystem.Infrastructure.Data;

using MassTransit;
using UniversityManagementSystem.Core.Events;
using NUlid;

namespace UniversityManagementSystem.Infrastructure.Services
{
    public class AttendanceService(AppDbContext context, IPublishEndpoint publishEndpoint, IAuditService auditService) : IAttendanceService
    {
        private readonly AppDbContext _context = context;
        private readonly IPublishEndpoint _publishEndpoint = publishEndpoint;
        private readonly IAuditService _auditService = auditService;

        public async Task<QrCodeResponseDto> CreateSessionAsync(CreateAttendanceSessionDto dto, Ulid profileId, string role)
        {
            // Validate Authorization
            if (role == UserRole.Doctor.ToString())
            {
                var isAssigned = await _context.SubjectDoctors.AnyAsync(sd => sd.SubjectId == dto.SubjectId && sd.DoctorId == profileId);
                if (!isAssigned) throw new UnauthorizedAccessException("You are not assigned to this subject.");
            }
            else if (role == UserRole.TeachingAssistant.ToString())
            {
                var isAssigned = await _context.SubjectAssistants.AnyAsync(sa => sa.SubjectId == dto.SubjectId && sa.TeachingAssistantId == profileId);
                if (!isAssigned) throw new UnauthorizedAccessException("You are not assigned to this subject.");
            }
            // Admin/SuperAdmin can bypass or we restrict them too? Usually Admin can do anything.
            // Requirement said "If user is Doctor... If user is TA...". Implicitly allow others or block?
            // "If validation fails -> return Forbidden". I'll assume Admins are fine or blocked if not Doctor/TA.
            // Let's assume Admin is allowed.

            var session = new AttendanceSession
            {
                SubjectId = dto.SubjectId,
                SessionDate = dto.SessionDate,
                StartTime = dto.StartTime,
                EndTime = dto.EndTime,
                QrCodeContent = Guid.NewGuid().ToString(), // Random secure token
                IsActive = true
            };

            _context.AttendanceSessions.Add(session);
            await _context.SaveChangesAsync();

            return new QrCodeResponseDto
            {
                SessionId = session.Id,
                QrContent = session.QrCodeContent,
                QrImageUrl = $"/api/qr/{session.QrCodeContent}" // Pseudo-url
            };
        }

        public async Task<IEnumerable<RecordAttendanceDto>> GetStudentAttendanceAsync(Ulid studentId, Ulid subjectId)
        {
            return await _context.StudentAttendances
                .Include(sa => sa.AttendanceSession)
                .Where(sa => sa.StudentId == studentId && sa.AttendanceSession.SubjectId == subjectId)
                .Select(sa => new RecordAttendanceDto
                {
                    SessionId = sa.AttendanceSessionId,
                    QrContent = sa.AttendanceSession.QrCodeContent
                })
                .ToListAsync();
        }

        public async Task<bool> RecordAttendanceAsync(Ulid studentId, RecordAttendanceDto dto)
        {
            // Note: We still do a read to validate the session exists and is active
            // to provide immediate feedback to the student if the QR is invalid.
            var session = await _context.AttendanceSessions
                .FirstOrDefaultAsync(s => s.Id == dto.SessionId && s.QrCodeContent == dto.QrContent && s.IsActive);

            if (session == null) return false;

            // Validate Enrollment
            var isEnrolled = await _context.Enrollments
                .Include(e => e.SubjectOffering)
                .AnyAsync(e => e.StudentId == studentId &&
                               e.SubjectOffering.SubjectId == session.SubjectId &&
                               e.IsActive);

            if (!isEnrolled) throw new Exception("Student not enrolled in any active offering of this subject.");

            // Offload the actual write to MassTransit
            await _publishEndpoint.Publish(new AttendanceRecordedEvent
            {
                StudentId = studentId,
                SessionId = dto.SessionId,
                QrContent = dto.QrContent,
                CheckInTime = DateTime.UtcNow
            });

            return true;
        }

        public async Task<AttendanceResponseDto> GetByIdAsync(Ulid sessionId, Ulid studentId)
        {
            var record = await _context.StudentAttendances
                .Include(sa => sa.Student)
                .FirstOrDefaultAsync(sa => sa.AttendanceSessionId == sessionId && sa.StudentId == studentId);

            if (record == null)
                throw new KeyNotFoundException("Attendance record not found.");

            return new AttendanceResponseDto
            {
                SessionId = record.AttendanceSessionId,
                StudentId = record.StudentId,
                StudentName = record.Student.FullName,
                CheckInTime = record.CheckInTime,
                IsPresent = record.IsPresent
            };
        }

        public async Task UpdateAttendanceAsync(Ulid sessionId, Ulid studentId, bool isPresent)
        {
            var record = await _context.StudentAttendances
                .FirstOrDefaultAsync(sa => sa.AttendanceSessionId == sessionId && sa.StudentId == studentId);

            var oldValues = record != null
                ? System.Text.Json.JsonSerializer.Serialize(new { record.IsPresent, record.CheckInTime })
                : "Absent";

            if (isPresent)
            {
                if (record == null)
                {
                    record = new StudentAttendance
                    {
                        AttendanceSessionId = sessionId,
                        StudentId = studentId,
                        CheckInTime = DateTime.UtcNow,
                        IsPresent = true
                    };
                    _context.StudentAttendances.Add(record);
                }
                else
                {
                    record.IsPresent = true;
                    _context.Entry(record).State = EntityState.Modified;
                }
            }
            else
            {
                if (record != null)
                {
                    // For attendance, "Absent" can be represented by soft delete if it's BaseEntity, 
                    // or just removing the record. The requirement says "Soft Delete".
                    // I'll set DeletedAt.
                    record.DeletedAt = DateTime.UtcNow;
                    _context.Entry(record).State = EntityState.Modified;
                }
            }

            await _context.SaveChangesAsync();

            var newValues = System.Text.Json.JsonSerializer.Serialize(new { IsPresent = isPresent });
            await _auditService.LogAsync("Update", "StudentAttendance", $"{sessionId}-{studentId}", oldValues, newValues, null);
        }

        public async Task DeleteAttendanceAsync(Ulid sessionId, Ulid studentId)
        {
            var record = await _context.StudentAttendances
                .FirstOrDefaultAsync(sa => sa.AttendanceSessionId == sessionId && sa.StudentId == studentId);

            if (record == null)
                throw new KeyNotFoundException("Attendance record not found.");

            var oldValues = System.Text.Json.JsonSerializer.Serialize(new { record.IsPresent, record.DeletedAt });

            record.DeletedAt = DateTime.UtcNow;
            _context.Entry(record).State = EntityState.Modified;
            await _context.SaveChangesAsync();

            await _auditService.LogAsync("SoftDelete", "StudentAttendance", $"{sessionId}-{studentId}", oldValues, null, null);
        }
    }
}
