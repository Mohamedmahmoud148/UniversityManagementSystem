using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniversityManagementSystem.Core.Application.AI.Execution;
using UniversityManagementSystem.Core.Application.AI.Logging;
using UniversityManagementSystem.Core.Application.AI.Security;
using UniversityManagementSystem.Infrastructure.Data;

namespace UniversityManagementSystem.Api.Controllers;

[Route("api/ai")]
[ApiController]
public class AiController : ControllerBase
{
    private readonly AiToolRegistry _toolRegistry;
    private readonly AppDbContext _context;

    public AiController(AiToolRegistry toolRegistry, AppDbContext context)
    {
        _toolRegistry = toolRegistry;
        _context = context;
    }

    [HttpPost("execute")]
    [Authorize]
    public async Task<IActionResult> Execute([FromBody] AiExecutionRequest request)
    {
        try
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var role = User.FindFirstValue(ClaimTypes.Role);

            if (string.IsNullOrEmpty(role))
            {
                return Forbid();
            }

            int userId = 0;
            if (!string.IsNullOrEmpty(userIdStr))
            {
                int.TryParse(userIdStr, out userId);
            }

            string parametersJson = string.Empty;
            try
            {
                // Safely serialize request.Parameters
                parametersJson = JsonSerializer.Serialize(request.Parameters);
            }
            catch
            {
                parametersJson = "{}"; // Fallback if serialization fails
            }

            if (!AiCapabilityMatrix.IsAllowed(role, request.ToolName))
            {
                return StatusCode(StatusCodes.Status403Forbidden, new AiExecutionResponse
                {
                    Success = false,
                    Message = "You do not have permission to execute this AI tool."
                });
            }

            var tool = _toolRegistry.GetTool(request.ToolName);
            if (tool == null)
            {
                return BadRequest(new AiExecutionResponse
                {
                    Success = false,
                    Message = $"AI tool '{request.ToolName}' not found or is currently unavailable."
                });
            }

            object? dataResult = null;
            bool executionSuccess = false;
            string returnMessage = string.Empty;

            try
            {
                dataResult = await tool.ExecuteAsync(request.Parameters, User);
                executionSuccess = true;
                returnMessage = "AI execution completed successfully.";
            }
            catch (Exception ex)
            {
                executionSuccess = false;
                returnMessage = $"An error occurred during AI execution: {ex.Message}";
            }

            // Safe Action Logging (Should not break the API response if it fails)
            try
            {
                var actionLog = new AiActionLog
                {
                    UserId = userId,
                    Role = role,
                    ToolName = request.ToolName,
                    ParametersJson = parametersJson,
                    Success = executionSuccess,
                    Timestamp = DateTime.UtcNow
                };

                _context.AiActionLogs.Add(actionLog);
                await _context.SaveChangesAsync();
            }
            catch
            {
                // Silently fail logging to avoid returning a 500 when tool execution succeeded
                // In a real production app, this might be forwarded to ILogger<T>.LogError
            }

            if (executionSuccess)
            {
                return Ok(new AiExecutionResponse
                {
                    Success = true,
                    Message = returnMessage,
                    Data = dataResult
                });
            }
            else
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new AiExecutionResponse
                {
                    Success = false,
                    Message = returnMessage
                });
            }
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new AiExecutionResponse
            {
                Success = false,
                Message = $"An unexpected error occurred: {ex.Message}"
            });
        }
    }
}
