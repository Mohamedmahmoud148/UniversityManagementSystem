using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using UniversityManagementSystem.Core.Entities;
using UniversityManagementSystem.Core.Events;
using UniversityManagementSystem.Infrastructure.Data;

namespace UniversityManagementSystem.Infrastructure.Consumers
{
    public class AttendanceConsumer(AppDbContext context, ILogger<AttendanceConsumer> logger) : IConsumer<AttendanceRecordedEvent>
    {
        private readonly AppDbContext _context = context;
        private readonly ILogger<AttendanceConsumer> _logger = logger;

        public async Task Consume(ConsumeContext<AttendanceRecordedEvent> context)
        {
            var data = context.Message;
            _logger.LogInformation("Processing attendance for Student {StudentId}, Session {SessionId}", data.StudentId, data.SessionId);

            var session = await _context.AttendanceSessions
                .FirstOrDefaultAsync(s => s.Id == data.SessionId && s.QrCodeContent == data.QrContent);

            if (session == null || !session.IsActive)
            {
                _logger.LogWarning("Invalid or inactive session {SessionId} for attendance recording.", data.SessionId);
                return;
            }

            var existing = await _context.StudentAttendances
                .AnyAsync(sa => sa.AttendanceSessionId == data.SessionId && sa.StudentId == data.StudentId);

            if (existing)
            {
                _logger.LogInformation("Attendance already exists for Student {StudentId} in Session {SessionId}", data.StudentId, data.SessionId);
                return;
            }

            var attendance = new StudentAttendance
            {
                AttendanceSessionId = data.SessionId,
                StudentId = data.StudentId,
                IsPresent = true,
                CheckInTime = data.CheckInTime
            };

            _context.StudentAttendances.Add(attendance);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Successfully recorded attendance for Student {StudentId} in Session {SessionId}", data.StudentId, data.SessionId);
        }
    }
}
