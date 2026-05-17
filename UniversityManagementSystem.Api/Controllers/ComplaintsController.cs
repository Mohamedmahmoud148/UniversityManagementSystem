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
    public class ComplaintsController : ControllerBase
    {
        private readonly IComplaintService _complaintService;
        private readonly IUserContextService _userContext;

        public ComplaintsController(IComplaintService complaintService, IUserContextService userContext)
        {
            _complaintService = complaintService;
            _userContext = userContext;
        }

        [HttpPost]
        [Authorize(Roles = "Student,SuperAdmin")]
        public async Task<IActionResult> CreateComplaint([FromBody] CreateComplaintDto dto)
        {
            var userId = _userContext.GetUserId();
            var result = await _complaintService.CreateComplaintAsync(userId, dto);
            return Ok(result);
        }

        [HttpGet("my-complaints")]
        [Authorize(Roles = "Student,SuperAdmin")]
        public async Task<IActionResult> GetMyComplaints([FromQuery] GetComplaintsQueryDto query)
        {
            var userId = _userContext.GetUserId();
            var result = await _complaintService.GetComplaintsAsync(query, userId, "Student");
            return Ok(result);
        }

        [HttpGet("my-reports")]
        [Authorize(Roles = "Doctor,SuperAdmin")]
        public async Task<IActionResult> GetDoctorReports([FromQuery] GetComplaintsQueryDto query)
        {
            var userId = _userContext.GetUserId();
            var result = await _complaintService.GetComplaintsAsync(query, userId, "Doctor");
            return Ok(result);
        }

        [HttpGet("all")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<IActionResult> GetAllComplaints([FromQuery] GetComplaintsQueryDto query)
        {
            var userId = _userContext.GetUserId();
            var result = await _complaintService.GetComplaintsAsync(query, userId, "Admin");
            return Ok(result);
        }

        [HttpGet("clusters")]
        [Authorize(Roles = "Admin,SuperAdmin,Doctor")]
        public async Task<IActionResult> GetClusters([FromQuery] string? targetType, [FromQuery] string? targetId)
        {
            var result = await _complaintService.GetClustersAsync(targetType, targetId);
            return Ok(result);
        }

        [HttpGet("doctor-options")]
        [Authorize(Roles = "Student,SuperAdmin")]
        public async Task<IActionResult> GetDoctorOptions()
        {
            var studentId = _userContext.GetProfileId();
            var result = await _complaintService.GetDoctorOptionsForStudentAsync(studentId);
            return Ok(result);
        }
    }
}
