using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using UniversityManagementSystem.Infrastructure.Data;

namespace UniversityManagementSystem.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public class DevController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IHostEnvironment _env;

        public DevController(AppDbContext context, IHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        [HttpGet("migrate-debug")]
        public async Task<IActionResult> DebugMigrations()
        {
            if (!_env.IsDevelopment())
                return Forbid();

            try
            {
                var pending = await _context.Database.GetPendingMigrationsAsync();
                var applied = await _context.Database.GetAppliedMigrationsAsync();

                try
                {
                    await _context.Database.MigrateAsync();
                    return Ok(new
                    {
                        Success = true,
                        Message = "Migrations applied successfully!",
                        PreviouslyApplied = applied,
                        JustApplied = pending
                    });
                }
                catch (Exception ex)
                {
                    return StatusCode(500, new
                    {
                        Success = false,
                        Message = "Migration failed.",
                        PendingMigrations = pending,
                        ExceptionMessage = ex.Message,
                        InnerException = ex.InnerException?.Message
                    });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Error = "Failed to get pending migrations", Details = ex.Message });
            }
        }
    }
}
