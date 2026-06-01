using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NUlid;
using UniversityManagementSystem.Core.DTOs.Companion;
using UniversityManagementSystem.Core.Interfaces;

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
    IUserContextService userContext) : ControllerBase
{
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
