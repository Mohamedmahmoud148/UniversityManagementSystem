using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;
using UniversityManagementSystem.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;

namespace UniversityManagementSystem.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DevController : ControllerBase
    {
        private readonly AppDbContext _context;

        public DevController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet("migrate-debug")]
        [AllowAnonymous]
        public async Task<IActionResult> DebugMigrations()
        {
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
                        StackTrace = ex.StackTrace,
                        InnerException = ex.InnerException?.Message,
                        FullDetails = ex.ToString()
                    });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Error = "Failed to even get pending migrations", Details = ex.ToString() });
            }
        }
    }
}
