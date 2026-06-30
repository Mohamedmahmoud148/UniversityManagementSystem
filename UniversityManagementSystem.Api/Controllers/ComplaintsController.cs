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

        [HttpPut("{id}/reply")]
        [Authorize(Roles = "Doctor,SuperAdmin")]
        public async Task<IActionResult> ReplyToComplaint(string id, [FromBody] ComplaintReplyDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            if (!Ulid.TryParse(id, out var complaintId))
                return BadRequest(new { error = "Invalid complaint ID." });

            var doctorSystemUserId = _userContext.GetUserId();

            try
            {
                var result = await _complaintService.ReplyToComplaintAsync(complaintId, dto.Reply, doctorSystemUserId);
                return Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { error = ex.Message });
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
        }

        [HttpPut("clusters/{clusterId}/reply")]
        [Authorize(Roles = "Admin,SuperAdmin,Doctor")]
        public async Task<IActionResult> ReplyToCluster(string clusterId, [FromBody] ClusterReplyRequestDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            if (!Ulid.TryParse(clusterId, out var id)) return BadRequest(new { error = "Invalid cluster ID." });

            var userId = _userContext.GetUserId();
            try
            {
                var result = await _complaintService.ReplyToClusterAsync(id, dto.Message, userId);
                return Ok(result);
            }
            catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
            catch (UnauthorizedAccessException) { return Forbid(); }
        }

        [HttpGet("clusters/{clusterId}")]
        [Authorize(Roles = "Admin,SuperAdmin,Doctor")]
        public async Task<IActionResult> GetCluster(string clusterId)
        {
            if (!Ulid.TryParse(clusterId, out var id)) return BadRequest(new { error = "Invalid cluster ID." });
            try
            {
                var result = await _complaintService.GetClusterByIdAsync(id);
                return Ok(result);
            }
            catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
        }

        [HttpPatch("clusters/{clusterId}/status")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<IActionResult> UpdateClusterStatus(string clusterId, [FromBody] UpdateClusterStatusDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            if (!Ulid.TryParse(clusterId, out var id)) return BadRequest(new { error = "Invalid cluster ID." });

            var userId = _userContext.GetUserId();
            try
            {
                await _complaintService.UpdateClusterStatusAsync(id, dto.Status, userId, dto.Reason);
                return NoContent();
            }
            catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
            catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
        }

        [HttpGet("dashboard")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<IActionResult> GetDashboard()
        {
            var result = await _complaintService.GetDashboardAsync();
            return Ok(result);
        }

        [HttpPost("reprocess-pending")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<IActionResult> ReprocessPending()
        {
            var count = await _complaintService.ReprocessUnanalyzedComplaintsAsync();
            return Ok(new { requeued = count });
        }
    }
}
