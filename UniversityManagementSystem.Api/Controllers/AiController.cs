using System.Security.Claims;
using System.Text;
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
public class AiController(AiToolRegistry toolRegistry, AppDbContext context, IHttpClientFactory httpFactory) : ControllerBase
{
    private readonly AiToolRegistry _toolRegistry = toolRegistry;
    private readonly AppDbContext _context = context;
    private readonly HttpClient _http = httpFactory.CreateClient();


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

            int userId = 0;
            if (!string.IsNullOrEmpty(userIdStr) && int.TryParse(userIdStr, out var parsedId))
            {
                userId = parsedId;
            }

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
            // CASE 2: Forward request to AI Orchestration Service
            // ------------------------------------------------

            else
            {
                try
                {
                    var token = Request.Headers.Authorization.ToString();

                    var aiRequest = new
                    {
                        message = $"{request.ToolName} {parametersJson}",
                        role = role.ToLower(),
                        conversation_id = Guid.NewGuid().ToString()
                    };

                    var json = JsonSerializer.Serialize(aiRequest);

                    var httpRequest = new HttpRequestMessage(
                        HttpMethod.Post,
                        "https://ai-orchestration-service-production.up.railway.app/api/chat"
                    );

                    httpRequest.Content = new StringContent(json, Encoding.UTF8, "application/json");

                    if (!string.IsNullOrEmpty(token))
                        httpRequest.Headers.Add("Authorization", token);

                    var response = await _http.SendAsync(httpRequest);

                    var result = await response.Content.ReadAsStringAsync();

                    dataResult = result;
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