using System.Collections.Generic;
using System.Threading.Tasks;
using NUlid;
using UniversityManagementSystem.Core.DTOs.Companion;
using UniversityManagementSystem.Core.Entities;

namespace UniversityManagementSystem.Core.Interfaces
{
    public interface IAiCompanionService
    {
        // ── Profile ───────────────────────────────────────────────────────
        Task<AiCompanionProfileDto> GetOrCreateProfileAsync(Ulid userId);
        Task<AiCompanionProfileDto> UpdateProfileAsync(Ulid userId, UpdateCompanionProfileDto dto);

        // ── Learning Sessions ─────────────────────────────────────────────
        Task<LearningSessionDto> StartSessionAsync(Ulid userId, StartLearningSessionDto dto);
        Task<LearningSessionDto> CompleteSessionAsync(Ulid sessionId, CompleteSessionDto dto);
        Task<IList<LearningSessionDto>> GetSessionHistoryAsync(Ulid userId, int page = 1, int pageSize = 20);

        // ── Flashcards ────────────────────────────────────────────────────
        Task<FlashcardDeckDto> GenerateFlashcardsAsync(Ulid userId, GenerateFlashcardsDto dto);
        Task<IList<FlashcardDeckDto>> GetMyDecksAsync(Ulid userId);
        Task<FlashcardDeckDto> GetDeckAsync(Ulid deckId);
        Task<FlashcardDto> ReviewCardAsync(Ulid cardId, ReviewFlashcardDto dto);
        Task<IList<FlashcardDto>> GetDueCardsAsync(Ulid userId, int limit = 20);

        // ── Insights ──────────────────────────────────────────────────────
        Task<IList<AiInsightDto>> GetMyInsightsAsync(Ulid userId, bool unreadOnly = false);
        Task AcknowledgeInsightAsync(Ulid insightId, Ulid userId);
        Task<CompanionDashboardDto> GetDashboardAsync(Ulid userId);

        // ── Analytics (doctor) ────────────────────────────────────────────
        Task<ClassAnalyticsDto> GetClassAnalyticsAsync(Ulid subjectOfferingId, Ulid doctorUserId);
        Task<IList<WeakTopicDto>> GetClassWeakTopicsAsync(Ulid subjectOfferingId, Ulid doctorUserId);
    }
}
