using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using UniversityManagementSystem.Infrastructure.Data;

namespace UniversityManagementSystem.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "SuperAdmin,Admin")]
    public class DashboardController : ControllerBase
    {
        private readonly AppDbContext _context;

        public DashboardController(AppDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// GET /api/Dashboard
        /// Returns system-wide statistics for the admin dashboard.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetStats()
        {
            var totalStudents = await _context.Students.CountAsync();
            var activeStudents = await _context.Students.CountAsync(s => s.IsActive);
            var totalDoctors = await _context.Doctors.CountAsync();
            var totalSubjects = await _context.Subjects.CountAsync();
            var totalExams = await _context.Exams.CountAsync();
            var totalColleges = await _context.Colleges.CountAsync();
            var totalDepartments = await _context.Departments.CountAsync();
            var totalBatches = await _context.Batches.CountAsync();
            var totalGroups = await _context.Groups.CountAsync();
            var totalAdmins = await _context.Admins.CountAsync();

            return Ok(new
            {
                TotalStudents = totalStudents,
                ActiveStudents = activeStudents,
                InactiveStudents = totalStudents - activeStudents,
                TotalDoctors = totalDoctors,
                TotalSubjects = totalSubjects,
                TotalExams = totalExams,
                TotalColleges = totalColleges,
                TotalDepartments = totalDepartments,
                TotalBatches = totalBatches,
                TotalGroups = totalGroups,
                TotalAdmins = totalAdmins
            });
        }
    }
}
