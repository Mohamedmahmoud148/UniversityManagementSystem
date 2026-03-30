using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NUlid;
using UniversityManagementSystem.Core.Application.AI.Execution;
using UniversityManagementSystem.Core.Application.AI.Logging;
using UniversityManagementSystem.Core.Application.AI.Security;
using UniversityManagementSystem.Core.DTOs;
using UniversityManagementSystem.Core.DTOs.Ai;
using UniversityManagementSystem.Core.Interfaces;
using UniversityManagementSystem.Infrastructure.Data;

namespace UniversityManagementSystem.Api.Controllers;

[Route("api/ai")]
[ApiController]
public class AiController(AiToolRegistry toolRegistry, AppDbContext context, IAiService aiService, IStudentFileService studentFileService, IUserContextService userContext) : ControllerBase
{
    private readonly AiToolRegistry _toolRegistry = toolRegistry;
    private readonly AppDbContext _context = context;
    private readonly IAiService _aiService = aiService;
    private readonly IStudentFileService _studentFileService = studentFileService;
    private readonly IUserContextService _userContext = userContext;


    [HttpPost("execute")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<IActionResult> Execute([FromBody] AiExecutionRequest request)
    {
        try
        {
            var userIdStr = _userContext.GetUserIdString();
            var role = _userContext.GetRole();

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

    // ── POST /api/ai/summarize ────────────────────────────────────────────
    /// <summary>
    /// Summarize a student file. Student role only.
    /// Sends extracted text if available; otherwise sends signed URL to AI.
    /// </summary>
    [HttpPost("summarize")]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> Summarize([FromBody] SummarizeRequestDto dto)
    {
        var studentId = ResolveStudentId();
        if (studentId == null) return Unauthorized("Student identity not found in token.");

        if (!Ulid.TryParse(dto.FileId, out var fileId))
            return BadRequest("Invalid fileId format.");

        try
        {
            var (content, isText) = await _studentFileService.GetFileContentForAiAsync(fileId, studentId.Value);

            AiResponseDto aiResult;
            if (isText)
            {
                aiResult = await _aiService.AnalyzeTextAsync(content);
            }
            else
            {
                var extraction = await _aiService.ExtractDataFromFileAsync(content, "application/pdf");
                aiResult = new AiResponseDto { Data = extraction.ExtractectedJson };
            }

            return Ok(new AiQueryResponseDto
            {
                FileId = dto.FileId,
                Result = aiResult.Data?.ToString() ?? string.Empty,
                UsedExtractedText = isText
            });
        }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    // ── POST /api/ai/ask ──────────────────────────────────────────────────
    /// <summary>
    /// Ask a question about a student file. Student role only.
    /// </summary>
    [HttpPost("ask")]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> Ask([FromBody] AskRequestDto dto)
    {
        var studentId = ResolveStudentId();
        if (studentId == null) return Unauthorized("Student identity not found in token.");

        if (!Ulid.TryParse(dto.FileId, out var fileId))
            return BadRequest("Invalid fileId format.");

        if (string.IsNullOrWhiteSpace(dto.Question))
            return BadRequest("Question cannot be empty.");

        try
        {
            var (content, isText) = await _studentFileService.GetFileContentForAiAsync(fileId, studentId.Value);

            var contextMessage = isText
                ? $"Using the following document:\n\n{content}\n\nAnswer: {dto.Question}"
                : $"Using the document at this URL: {content}\n\nAnswer: {dto.Question}";

            var chatRequest = new AiChatRequestDto { message = contextMessage };
            var chatResponse = await _aiService.SendChatMessageAsync(chatRequest);

            return Ok(new AiQueryResponseDto
            {
                FileId = dto.FileId,
                Result = chatResponse.response ?? string.Empty,
                UsedExtractedText = isText
            });
        }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    // ── Private: resolve student ID from JWT via IUserContextService ───────────────
    private NUlid.Ulid? ResolveStudentId()
    {
        var raw = _userContext.TryGetProfileId();
        if (!string.IsNullOrEmpty(raw) && NUlid.Ulid.TryParse(raw, out var pid))
            return pid;

        // Fallback: try userId claim
        try { return _userContext.GetUserId(); }
        catch { return null; }
    }
}