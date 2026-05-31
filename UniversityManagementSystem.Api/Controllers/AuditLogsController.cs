using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;
using NUlid;
using UniversityManagementSystem.Infrastructure.Data;

namespace UniversityManagementSystem.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "SuperAdmin,Admin")]
    public class AuditLogsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public AuditLogsController(AppDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// GET /api/AuditLogs?page=1&amp;pageSize=20&amp;entity=Student&amp;userId=
        /// Returns paginated audit log entries. SuperAdmin/Admin only.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetAll(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string? entity = null,
            [FromQuery] string? userId = null,
            [FromQuery] string? action = null)
        {
            if (page < 1) page = 1;
            if (pageSize is < 1 or > 100) pageSize = 20;

            var query = _context.AuditLogs.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(entity))
                query = query.Where(l => l.EntityName.ToLower() == entity.ToLower());

            if (!string.IsNullOrWhiteSpace(userId) && Ulid.TryParse(userId, out var uid))
                query = query.Where(l => l.PerformedByUserId == uid);

            if (!string.IsNullOrWhiteSpace(action))
                query = query.Where(l => l.ActionType.ToLower() == action.ToLower());

            var total = await query.CountAsync();

            var logs = await query
                .OrderByDescending(l => l.PerformedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(l => new
                {
                    Id              = l.Id.ToString(),
                    l.ActionType,
                    l.EntityName,
                    EntityId        = l.EntityId.ToString(),
                    l.OldValues,
                    l.NewValues,
                    PerformedByUserId = l.PerformedByUserId.HasValue
                        ? l.PerformedByUserId.Value.ToString()
                        : null,
                    l.PerformedAt
                })
                .ToListAsync();

            var totalPages = (int)Math.Ceiling(total / (double)pageSize);

            return Ok(new
            {
                page,
                pageSize,
                size = pageSize,
                total,
                totalCount = total,
                totalPages,
                hasNextPage = page * pageSize < total,
                data = logs,
                items = logs
            });
        }
    }
}
