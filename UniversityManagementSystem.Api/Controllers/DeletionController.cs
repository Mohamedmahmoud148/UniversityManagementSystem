using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NUlid;
using UniversityManagementSystem.Core.DTOs;
using UniversityManagementSystem.Core.Interfaces;

namespace UniversityManagementSystem.Api.Controllers
{
    /// <summary>
    /// Intelligent Deletion Framework — analyze impact before delete, then execute safely.
    /// All operations require Admin or SuperAdmin role.
    /// </summary>
    [Authorize(Roles = "Admin,SuperAdmin")]
    [ApiController]
    [Route("api/deletion")]
    public class DeletionController(
        IDeletionService deletionService,
        IUserContextService userContext) : ControllerBase
    {
        // ────────────────────────────────────────────────────────────────────
        // POST /api/deletion/analyze
        // Step 1: Analyze impact before any delete is executed.
        // ────────────────────────────────────────────────────────────────────
        [HttpPost("analyze")]
        public async Task<ActionResult<ApiResponse<DeleteAnalysisResponseDto>>> Analyze(
            [FromBody] DeleteAnalysisRequestDto request)
        {
            if (string.IsNullOrWhiteSpace(request.EntityName))
                return BadRequest(ApiResponse<DeleteAnalysisResponseDto>.FailureResponse("EntityName is required."));

            if (!Ulid.TryParse(request.EntityId, out var entityId))
                return BadRequest(ApiResponse<DeleteAnalysisResponseDto>.FailureResponse("Invalid EntityId format."));

            var result = await deletionService.AnalyzeAsync(request.EntityName, entityId);
            return Ok(ApiResponse<DeleteAnalysisResponseDto>.SuccessResponse(result,
                $"Impact analysis complete for {result.DisplayName}"));
        }

        // ────────────────────────────────────────────────────────────────────
        // POST /api/deletion/execute
        // Step 2: Execute delete after frontend has obtained user confirmation.
        // ────────────────────────────────────────────────────────────────────
        [HttpPost("execute")]
        public async Task<ActionResult<ApiResponse<DeleteExecutionResponseDto>>> Execute(
            [FromBody] DeleteExecutionRequestDto request)
        {
            if (string.IsNullOrWhiteSpace(request.EntityName))
                return BadRequest(ApiResponse<DeleteExecutionResponseDto>.FailureResponse("EntityName is required."));

            if (!Ulid.TryParse(request.EntityId, out _))
                return BadRequest(ApiResponse<DeleteExecutionResponseDto>.FailureResponse("Invalid EntityId format."));

            var userId = userContext.GetUserId();

            try
            {
                var result = await deletionService.ExecuteAsync(request, userId);
                return Ok(ApiResponse<DeleteExecutionResponseDto>.SuccessResponse(result, result.Message));
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(ApiResponse<DeleteExecutionResponseDto>.FailureResponse(ex.Message));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ApiResponse<DeleteExecutionResponseDto>.FailureResponse(ex.Message));
            }
        }
    }
}
