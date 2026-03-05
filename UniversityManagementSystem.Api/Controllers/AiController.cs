using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NUlid;
using UniversityManagementSystem.Core.Application.AI.Execution;
using UniversityManagementSystem.Core.Application.AI.Logging;
using UniversityManagementSystem.Core.Application.AI.Security;
using UniversityManagementSystem.Core.DTOs.Ai;
using UniversityManagementSystem.Core.Interfaces;
using UniversityManagementSystem.Infrastructure.Data;

namespace UniversityManagementSystem.Api.Controllers;

[Route("api/ai")]
[ApiController]
public class AiController(AiToolRegistry toolRegistry, AppDbContext context, IAiService aiService) : ControllerBase
{
    private readonly AiToolRegistry _toolRegistry = toolRegistry;
    private readonly AppDbContext _context = context;
    private readonly IAiService _aiService = aiService;


    [HttpPost("execute")]
    [Authorize(Roles = "Admin,SuperAdmin")]
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

            Ulid userId = Ulid.Empty;
            if (!string.IsNullOrEmpty(userIdStr))
                Ulid.TryParse(userIdStr, out userId);

            string parametersJson = JsonSerializer.Serialize(request.Parameters ?? new Dictionary<string, object>());

            if (!AiCapabilityMatrix.IsAllowed(role, request.ToolName))
            {
                return StatusCode(StatusCodes.Status403Forbidden, new AiExecutionResponse
                {
                    Success = false,
                    Message = "You do not have permission to execute this AI tool."
                });
            }

            var tool = _toolRegistry.GetTool(request.ToolName);

            object? dataResult = null;
            bool executionSuccess = false;
            string returnMessage = "";

            // ------------------------------------------------
            // CASE 1: Tool exists locally in backend
            // ------------------------------------------------

            if (tool != null)
            {
                try
                {
                    dataResult = await tool.ExecuteAsync(request.Parameters ?? new Dictionary<string, object>(), User);
                    executionSuccess = true;
                    returnMessage = "AI execution completed locally.";
                }
                catch (Exception ex)
                {
                    executionSuccess = false;
                    returnMessage = $"Local AI execution error: {ex.Message}";
                }
            }

            // ------------------------------------------------
            // CASE 2: Forward request to AI Orchestration Service via IAiService
            // ------------------------------------------------

            else
            {
                try
                {
                    var aiRequest = new AiChatRequestDto
                    {
                        message = $"{request.ToolName} {parametersJson}",
                        user_id = userId,
                        role = role.ToLower(),
                        conversation_id = Guid.NewGuid().ToString(),
                        history = [],
                        academic_context = new { }
                    };

                    var aiResponse = await _aiService.SendChatMessageAsync(aiRequest);

                    dataResult = aiResponse;
                    executionSuccess = true;
                    returnMessage = "AI execution handled by AI Service.";
                }
                catch (Exception ex)
                {
                    executionSuccess = false;
                    returnMessage = $"AI Service error: {ex.Message}";
                }
            }

            // ------------------------------------------------
            // LOGGING
            // ------------------------------------------------

            try
            {
                AiActionLog actionLog = new()
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
                // silent logging failure
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

            return StatusCode(StatusCodes.Status500InternalServerError, new AiExecutionResponse
            {
                Success = false,
                Message = returnMessage
            });
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new AiExecutionResponse
            {
                Success = false,
                Message = $"Unexpected error: {ex.Message}"
            });
        }
    }
}