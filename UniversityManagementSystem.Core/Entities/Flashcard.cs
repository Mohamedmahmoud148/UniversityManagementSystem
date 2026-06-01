using System;
using System.Collections.Generic;
using NUlid;

namespace UniversityManagementSystem.Core.Entities
{
    public enum FlashcardDifficulty { Easy = 0, Medium = 1, Hard = 2 }

    /// <summary>
    /// A set of AI-generated flashcards for a specific topic/subject.
    /// Supports spaced repetition by tracking review history.
    /// </summary>
    public class FlashcardDeck : BaseEntity
    {
        public Ulid UserId { get; set; }
        public Ulid? SubjectOfferingId { get; set; }

        public string Title { get; set; } = string.Empty;
        public string TopicName { get; set; } = string.Empty;
        /// AI model used to generate this deck
        public string GeneratedBy { get; set; } = "ai";
        public int CardCount { get; set; } = 0;

        // Navigation
        public SystemUser User { get; set; } = null!;
        public SubjectOffering? SubjectOffering { get; set; }
        public ICollection<Flashcard> Cards { get; set; } = new List<Flashcard>();
    }

    /// <summary>Single flashcard with spaced-repetition tracking.</summary>
    public class Flashcard : BaseEntity
    {
        public Ulid DeckId { get; set; }

        public string Front { get; set; } = string.Empty;   // Question / term
        public string Back { get; set; } = string.Empty;    // Answer / definition
        public string? Hint { get; set; }
        public FlashcardDifficulty Difficulty { get; set; } = FlashcardDifficulty.Medium;

        // ── Spaced repetition state (SM-2 algorithm) ──────────────────────
        /// Number of successful recalls in a row
        public int RepetitionCount { get; set; } = 0;
        /// Ease factor (SM-2): starts at 2.5, adjusted by recall quality
        public double EaseFactor { get; set; } = 2.5;
        /// Interval in days until next review
        public int IntervalDays { get; set; } = 1;
        /// When this card should next be reviewed
        public DateTime NextReviewAt { get; set; } = DateTime.UtcNow;
        /// Last time the user reviewed this card
        public DateTime? LastReviewedAt { get; set; }

        // Navigation
        public FlashcardDeck Deck { get; set; } = null!;
    }
}
