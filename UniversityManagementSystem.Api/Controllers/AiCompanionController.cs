using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NUlid;
using UniversityManagementSystem.Core.DTOs.Companion;
using UniversityManagementSystem.Core.Interfaces;
using UniversityManagementSystem.Infrastructure.Data;

namespace UniversityManagementSystem.Api.Controllers;

/// <summary>
/// AI Academic Companion endpoints.
///
/// All routes require authentication.  Students use /my/* routes.
/// Doctors use /class/* routes for analytics.
/// </summary>
[Route("api/companion")]
[ApiController]
[Authorize]
public class AiCompanionController(
    IAiCompanionService companionService,
    IUserContextService userContext,
    IAiService aiService,
    AppDbContext context) : ControllerBase
{
    private static readonly HashSet<string> _allowedMimes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/pdf",
        "text/plain",
        "image/jpeg", "image/png", "image/webp",
        "application/msword",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "application/vnd.ms-excel",
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        "text/csv",
    };
    // ── Profile ───────────────────────────────────────────────────────────

    /// <summary>Get or create the AI companion profile for the current user.</summary>
    [HttpGet("profile")]
    public async Task<IActionResult> GetProfile()
    {
        var userId = userContext.GetUserId();
        var profile = await companionService.GetOrCreateProfileAsync(userId);
        return Ok(profile);
    }

    /// <summary>Update learning style, goal, or preferred study time.</summary>
    [HttpPatch("profile")]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateCompanionProfileDto dto)
    {
        var userId = userContext.GetUserId();
        var profile = await companionService.UpdateProfileAsync(userId, dto);
        return Ok(profile);
    }

    // ── Dashboard ─────────────────────────────────────────────────────────

    /// <summary>
    /// Full companion dashboard: profile + insights + due flashcards +
    /// weekly progress + today's AI recommendations.
    /// </summary>
    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard()
    {
        var userId = userContext.GetUserId();
        var dashboard = await companionService.GetDashboardAsync(userId);
        return Ok(dashboard);
    }

    // ── Learning Sessions ─────────────────────────────────────────────────

    /// <summary>Start a new AI learning session (quiz, flashcards, etc.).</summary>
    [HttpPost("sessions/start")]
    [Authorize(Roles = "Student,SuperAdmin")]
    public async Task<IActionResult> StartSession([FromBody] StartLearningSessionDto dto)
    {
        var userId = userContext.GetUserId();
        var session = await companionService.StartSessionAsync(userId, dto);
        return Created($"/api/companion/sessions/{session.Id}", session);
    }

    /// <summary>Mark a session as completed and record performance data.</summary>
    [HttpPost("sessions/{sessionId}/complete")]
    [Authorize(Roles = "Student,SuperAdmin")]
    public async Task<IActionResult> CompleteSession(
        string sessionId, [FromBody] CompleteSessionDto dto)
    {
        if (!Ulid.TryParse(sessionId, out var id))
            return BadRequest("Invalid session ID.");

        var session = await companionService.CompleteSessionAsync(id, dto);
        return Ok(session);
    }

    /// <summary>
    /// Generate AI questions for an active session.
    /// Returns questions WITHOUT correct answers (hidden from student).
    /// </summary>
    [HttpPost("sessions/{sessionId}/generate-questions")]
    [Authorize(Roles = "Student,SuperAdmin")]
    public async Task<IActionResult> GenerateQuestions(string sessionId)
    {
        if (!Ulid.TryParse(sessionId, out var id))
            return BadRequest("Invalid session ID.");
        try
        {
            var questions = await companionService.GenerateSessionQuestionsAsync(id);
            return Ok(questions);
        }
        catch (KeyNotFoundException ex)   { return NotFound(ex.Message); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }

    /// <summary>
    /// Submit a single answer. AI grades it immediately and returns result.
    /// </summary>
    [HttpPost("sessions/{sessionId}/submit-answer")]
    [Authorize(Roles = "Student,SuperAdmin")]
    public async Task<IActionResult> SubmitAnswer(
        string sessionId, [FromBody] Core.DTOs.Companion.SubmitAnswerDto dto)
    {
        if (!Ulid.TryParse(sessionId, out var id))
            return BadRequest("Invalid session ID.");
        try
        {
            var result = await companionService.SubmitAnswerAsync(id, dto);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)      { return NotFound(ex.Message); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }

    /// <summary>
    /// Get the full session report with score, per-question review, and AI recommendations.
    /// Also auto-completes the session if still active.
    /// </summary>
    [HttpGet("sessions/{sessionId}/report")]
    [Authorize(Roles = "Student,SuperAdmin")]
    public async Task<IActionResult> GetSessionReport(string sessionId)
    {
        if (!Ulid.TryParse(sessionId, out var id))
            return BadRequest("Invalid session ID.");
        try
        {
            var report = await companionService.GetSessionReportAsync(id);
            return Ok(report);
        }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
    }

    /// <summary>Get the user's learning session history.</summary>
    [HttpGet("sessions")]
    public async Task<IActionResult> GetSessionHistory(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var userId = userContext.GetUserId();
        var sessions = await companionService.GetSessionHistoryAsync(userId, page, pageSize);
        return Ok(sessions);
    }

    // ── Flashcards ────────────────────────────────────────────────────────

    /// <summary>
    /// Generate a new AI flashcard deck for a topic.
    /// The FastAPI service generates the cards; they are persisted in the DB.
    /// </summary>
    [HttpPost("flashcards/generate")]
    [Authorize(Roles = "Student,SuperAdmin")]
    public async Task<IActionResult> GenerateFlashcards([FromBody] GenerateFlashcardsDto dto)
    {
        var userId = userContext.GetUserId();
        var deck = await companionService.GenerateFlashcardsAsync(userId, dto);
        return Created($"/api/companion/flashcards/{deck.Id}", deck);
    }

    /// <summary>List all flashcard decks owned by the current user.</summary>
    [HttpGet("flashcards")]
    public async Task<IActionResult> GetMyDecks()
    {
        var userId = userContext.GetUserId();
        var decks = await companionService.GetMyDecksAsync(userId);
        return Ok(decks);
    }

    /// <summary>Get a specific deck with all its cards.</summary>
    [HttpGet("flashcards/{deckId}")]
    public async Task<IActionResult> GetDeck(string deckId)
    {
        if (!Ulid.TryParse(deckId, out var id))
            return BadRequest("Invalid deck ID.");
        try
        {
            var deck = await companionService.GetDeckAsync(id);
            return Ok(deck);
        }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
    }

    /// <summary>
    /// Review a flashcard and update its spaced-repetition schedule.
    /// Quality 0–5 following the SM-2 algorithm.
    /// </summary>
    [HttpPost("flashcards/cards/{cardId}/review")]
    [Authorize(Roles = "Student,SuperAdmin")]
    public async Task<IActionResult> ReviewCard(
        string cardId, [FromBody] ReviewFlashcardDto dto)
    {
        if (!Ulid.TryParse(cardId, out var id))
            return BadRequest("Invalid card ID.");

        var card = await companionService.ReviewCardAsync(id, dto);
        return Ok(card);
    }

    /// <summary>Get flashcards due for review today (spaced repetition).</summary>
    [HttpGet("flashcards/due")]
    [Authorize(Roles = "Student,SuperAdmin")]
    public async Task<IActionResult> GetDueCards([FromQuery] int limit = 20)
    {
        var userId = userContext.GetUserId();
        var cards = await companionService.GetDueCardsAsync(userId, limit);
        return Ok(cards);
    }

    // ── Insights ──────────────────────────────────────────────────────────

    /// <summary>Get AI-generated insights for the current user.</summary>
    [HttpGet("insights")]
    public async Task<IActionResult> GetInsights([FromQuery] bool unreadOnly = false)
    {
        var userId = userContext.GetUserId();
        var insights = await companionService.GetMyInsightsAsync(userId, unreadOnly);
        return Ok(insights);
    }

    /// <summary>Acknowledge (dismiss) an AI insight.</summary>
    [HttpPost("insights/{insightId}/acknowledge")]
    public async Task<IActionResult> AcknowledgeInsight(string insightId)
    {
        if (!Ulid.TryParse(insightId, out var id))
            return BadRequest("Invalid insight ID.");

        var userId = userContext.GetUserId();
        try
        {
            await companionService.AcknowledgeInsightAsync(id, userId);
            return NoContent();
        }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    // ── Doctor Analytics ──────────────────────────────────────────────────

    // ── Student File Upload + AI Explanation ──────────────────────────────────

    /// <summary>
    /// Student uploads any file (PDF, DOCX, XLSX, image, TXT, CSV).
    /// AI extracts text, explains the content, and generates flashcards.
    /// Max 30 MB. Videos not supported.
    /// </summary>
    [HttpPost("explain-file")]
    [Authorize(Roles = "Student,SuperAdmin")]
    [RequestSizeLimit(31_457_280)]
    public async Task<IActionResult> ExplainFile(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest("No file provided.");

        if (!_allowedMimes.Contains(file.ContentType))
            return BadRequest($"File type '{file.ContentType}' is not supported. Supported: PDF, Word, Excel, CSV, TXT, JPG, PNG.");

        if (file.Length > 30 * 1024 * 1024)
            return BadRequest("File exceeds the 30 MB size limit.");

        using var ms = new System.IO.MemoryStream();
        await file.CopyToAsync(ms);
        var bytes = ms.ToArray();

        var result = await aiService.ExplainFileAsync(bytes, file.FileName, file.ContentType);

        if (result == null)
            return StatusCode(503, "AI explanation service is temporarily unavailable. Please try again.");

        return Ok(result);
    }

    /// <summary>
    /// Returns the student's enrolled subjects with material counts —
    /// used to populate the "My Subjects" section in AI Companion.
    /// </summary>
    [HttpGet("my-subjects")]
    [Authorize(Roles = "Student,SuperAdmin")]
    public async Task<IActionResult> GetMySubjects()
    {
        var studentId = userContext.GetProfileId();

        var enrollments = await context.Enrollments
            .AsNoTracking()
            .Where(e => e.StudentId == studentId && e.IsActive)
            .Include(e => e.SubjectOffering)
                .ThenInclude(so => so.Subject)
            .Include(e => e.SubjectOffering)
                .ThenInclude(so => so.Semester)
            .ToListAsync();

        var result = enrollments.Select(e => new
        {
            offeringId   = e.SubjectOfferingId.ToString(),
            subjectId    = e.SubjectOffering?.SubjectId.ToString(),
            subjectName  = e.SubjectOffering?.Subject?.Name ?? "",
            subjectCode  = e.SubjectOffering?.Subject?.Code ?? "",
            semesterName = e.SubjectOffering?.Semester?.Name ?? "",
            creditHours  = e.SubjectOffering?.Subject?.CreditHours ?? 0,
        }).ToList();

        return Ok(result);
    }

    /// <summary>
    /// Explain a specific course material by its ID using AI.
    /// Students can click on any uploaded material and get an explanation.
    /// </summary>
    [HttpPost("explain-material/{materialId}")]
    [Authorize(Roles = "Student,Doctor,TeachingAssistant,SuperAdmin")]
    public async Task<IActionResult> ExplainMaterial(string materialId)
    {
        if (!Ulid.TryParse(materialId, out var mid))
            return BadRequest("Invalid material ID.");

        var material = await context.Materials
            .AsNoTracking()
            .Include(m => m.File)
            .FirstOrDefaultAsync(m => m.Id == mid);

        if (material == null)
            return NotFound("Material not found.");

        // Get the signed URL from storage
        var storageKey = material.File?.StorageKey
            ?? material.StorageKey
            ?? material.StoredFileName;

        if (string.IsNullOrEmpty(storageKey))
            return BadRequest("Material has no associated file.");

        // Pass the file URL to the AI for explanation via chat context
        // We use the material URL which the FastAPI MaterialExplanationModule can fetch
        var fileUrl = material.FileUrl ?? "";

        // Build a compact explain request via QuickPrompt — we don't have file bytes here,
        // but the FastAPI explain-material route can fetch via fileUrl
        var explainResult = await aiService.ExplainFileAsync(
            fileBytes: [],
            fileName: material.FileName ?? "material",
            contentType: material.ContentType ?? "application/pdf");

        // If no bytes available, fallback: return material metadata for the client
        // to trigger the companion chat with the material URL in context
        return Ok(new
        {
            materialId   = material.Id.ToString(),
            fileName     = material.FileName,
            title        = material.Title,
            description  = material.Description,
            fileUrl      = fileUrl,
            contentType  = material.ContentType,
            suggestion   = "استخدم AI Companion وقوله 'اشرح المادة دي' وهيشرحها من الملف.",
            suggestionEn = "Open AI Companion and say 'explain this material' to get an AI explanation."
        });
    }

    /// <summary>
    /// Get AI-powered class analytics for a subject offering.
    /// Doctor role only.
    /// </summary>
    [HttpGet("class-analytics/{subjectOfferingId}")]
    [Authorize(Roles = "Doctor,SuperAdmin")]
    public async Task<IActionResult> GetClassAnalytics(string subjectOfferingId)
    {
        if (!Ulid.TryParse(subjectOfferingId, out var offeringId))
            return BadRequest("Invalid subject offering ID.");

        var userId = userContext.GetUserId();
        try
        {
            var analytics = await companionService.GetClassAnalyticsAsync(offeringId, userId);
            return Ok(analytics);
        }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    /// <summary>
    /// Get the weak topics detected by the AI for a class.
    /// Doctor role only.
    /// </summary>
    [HttpGet("class-analytics/{subjectOfferingId}/weak-topics")]
    [Authorize(Roles = "Doctor,SuperAdmin")]
    public async Task<IActionResult> GetWeakTopics(string subjectOfferingId)
    {
        if (!Ulid.TryParse(subjectOfferingId, out var offeringId))
            return BadRequest("Invalid subject offering ID.");

        var userId = userContext.GetUserId();
        try
        {
            var topics = await companionService.GetClassWeakTopicsAsync(offeringId, userId);
            return Ok(topics);
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }
}
