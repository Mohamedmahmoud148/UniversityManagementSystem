using System;
using NUlid;

namespace UniversityManagementSystem.Core.Entities
{
    public enum LearningSessionType
    {
        Quiz            = 0,   // AI-generated quiz session
        Flashcards      = 1,   // Spaced repetition flashcard session
        ConceptReview   = 2,   // Concept explanation + Q&A
        ExamPrep        = 3,   // Exam preparation session
        WeaknessReview  = 4,   // Targeted review of weak topics
        FreeStudy       = 5,   // Open-ended study chat
    }

    public enum LearningSessionStatus
    {
        Active      = 0,
        Completed   = 1,
        Abandoned   = 2,
    }

    /// <summary>
    /// Represents a single AI-driven learning session.
    /// Tracks what the student did, how they performed, and AI feedback.
    /// Used to build the engagement score and detect patterns.
    /// </summary>
    public class LearningSession : BaseEntity
    {
        public Ulid UserId { get; set; }
        public Ulid? SubjectOfferingId { get; set; }
        public Ulid AiCompanionProfileId { get; set; }

        public LearningSessionType SessionType { get; set; }
        public LearningSessionStatus Status { get; set; } = LearningSessionStatus.Active;

        /// Subject or topic the session is about
        public string TopicName { get; set; } = string.Empty;
        /// For quiz sessions: total questions asked
        public int TotalQuestions { get; set; } = 0;
        /// For quiz sessions: correct answers
        public int CorrectAnswers { get; set; } = 0;
        /// Computed accuracy 0–100 (set on completion)
        public double AccuracyPercent { get; set; } = 0;
        /// Duration in minutes (set on completion)
        public int DurationMinutes { get; set; } = 0;
        /// AI-generated feedback summary for this session
        public string AiFeedback { get; set; } = string.Empty;
        /// JSON: session transcript / Q&A pairs stored for review
        public string SessionData { get; set; } = "{}";

        public DateTime StartedAt { get; set; } = DateTime.UtcNow;
        public DateTime? CompletedAt { get; set; }

        // Navigation
        public SystemUser User { get; set; } = null!;
        public AiCompanionProfile AiCompanionProfile { get; set; } = null!;
        public SubjectOffering? SubjectOffering { get; set; }
    }
}
