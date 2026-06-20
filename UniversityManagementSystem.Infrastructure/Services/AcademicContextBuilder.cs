using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using NUlid;
using UniversityManagementSystem.Infrastructure.Data;

namespace UniversityManagementSystem.Infrastructure.Services
{
    /// <summary>
    /// Single source of truth for building academic context sent to the AI service.
    ///
    /// SERIALIZATION CONTRACT:
    ///   .NET serializes this with JsonNamingPolicy.SnakeCaseLower.
    ///   FastAPI receives: { "user_id": "...", "batch_id": "...", "student_id": "..." }
    ///   FastAPI normalizes via _normalize_academic_context() in react_agent.py
    ///   which adds camelCase aliases: { batch_id: "...", batchId: "..." }
    ///
    ///   This is the OFFICIAL contract. Do NOT change serialization policy without
    ///   updating both sides and the FastAPI normalization layer.
    ///
    /// EXTRACTION:
    ///   Moved out of ChatService and ChatStreamingService to eliminate duplication.
    ///   Both services now call BuildAsync() from this class.
    /// </summary>
    public class AcademicContextBuilder(AppDbContext context)
    {
        public async Task<object> BuildAsync(
            Ulid userId, string role, string? profileId,
            CancellationToken ct = default)
        {
            try
            {
                if (role.Equals("student", StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrEmpty(profileId)
                    && Ulid.TryParse(profileId, out var sid))
                {
                    var s = await context.Students.AsNoTracking()
                        .Include(x => x.Batch)
                        .Include(x => x.Department)
                        .Include(x => x.College)
                        .Include(x => x.Regulation)
                        .FirstOrDefaultAsync(x => x.Id == sid, ct);

                    if (s != null)
                        return new
                        {
                            userId         = userId.ToString(),
                            studentId      = sid.ToString(),
                            studentName    = s.FullName,
                            batchId        = s.BatchId.ToString(),
                            batchName      = s.Batch?.Name ?? "",
                            departmentId   = s.DepartmentId.ToString(),
                            departmentName = s.Department?.Name ?? "",
                            collegeName    = s.College?.Name ?? "",
                            regulationId   = s.RegulationId?.ToString() ?? "",
                            role
                        };
                }

                if (role.Equals("doctor", StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrEmpty(profileId)
                    && Ulid.TryParse(profileId, out var did))
                {
                    var d = await context.Doctors.AsNoTracking()
                        .Include(x => x.Department).ThenInclude(x => x.College)
                        .FirstOrDefaultAsync(x => x.Id == did, ct);

                    if (d != null)
                        return new
                        {
                            userId         = userId.ToString(),
                            doctorId       = did.ToString(),
                            doctorName     = d.FullName,
                            departmentId   = d.DepartmentId.ToString(),
                            departmentName = d.Department?.Name ?? "",
                            collegeName    = d.Department?.College?.Name ?? "",
                            role
                        };
                }

                if ((role.Equals("admin", StringComparison.OrdinalIgnoreCase)
                     || role.Equals("superadmin", StringComparison.OrdinalIgnoreCase))
                    && !string.IsNullOrEmpty(profileId)
                    && Ulid.TryParse(profileId, out var aid))
                {
                    var a = await context.Admins.AsNoTracking()
                        .FirstOrDefaultAsync(x => x.Id == aid, ct);

                    if (a != null)
                        return new
                        {
                            userId    = userId.ToString(),
                            adminId   = aid.ToString(),
                            adminName = a.FullName,
                            role
                        };
                }
            }
            catch
            {
                // Non-fatal — AI still works without full context
            }

            return new { userId = userId.ToString(), role };
        }
    }
}
