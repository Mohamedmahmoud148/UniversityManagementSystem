using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NUlid;
using UniversityManagementSystem.Core.Application.AI.Logging;
using UniversityManagementSystem.Core.Application.AI.Security;
using UniversityManagementSystem.Core.DTOs;
using UniversityManagementSystem.Core.DTOs.Ai;
using UniversityManagementSystem.Core.Interfaces;
using UniversityManagementSystem.Infrastructure.Data;

namespace UniversityManagementSystem.Api.Controllers;

[Route("api/ai")]
[ApiController]
public class AiController(AppDbContext context, IAiService aiService, IStudentFileService studentFileService, IUserContextService userContext) : ControllerBase
{
    private readonly AppDbContext _context = context;
    private readonly IAiService _aiService = aiService;
    private readonly IStudentFileService _studentFileService = studentFileService;
    private readonly IUserContextService _userContext = userContext;



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

            var chatRequest = new AiChatRequestDto { Message = contextMessage };
            var chatResponse = await _aiService.SendChatMessageAsync(chatRequest);

            return Ok(new AiQueryResponseDto
            {
                FileId = dto.FileId,
                Result = chatResponse.Response ?? string.Empty,
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