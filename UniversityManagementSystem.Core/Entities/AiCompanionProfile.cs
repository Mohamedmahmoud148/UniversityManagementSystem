using System;
using System.Collections.Generic;
using NUlid;

namespace UniversityManagementSystem.Core.Entities
{
    /// <summary>
    /// Persistent AI companion profile for a student or doctor.
    /// Stores long-term learned preferences, strengths, weaknesses, and goals
    /// so the AI can personalize every interaction across sessions.
    /// </summary>
    public class AiCompanionProfile : BaseEntity
    {
        public Ulid UserId { get; set; }

        // ── Academic profile (students) ───────────────────────────────────
        /// Comma-separated subject names the AI identified as weak areas
        public string WeakSubjects { get; set; } = string.Empty;
        /// Comma-separated subject names the AI identified as strong areas
        public string StrongSubjects { get; set; } = string.Empty;
        /// Detected learning style: "visual", "practical", "reading", "mixed"
        public string LearningStyle { get; set; } = "mixed";
        /// Current self-reported or AI-inferred academic goal
        public string CurrentGoal { get; set; } = string.Empty;
        /// Preferred study time: "morning", "evening", "night", "flexible"
        public string PreferredStudyTime { get; set; } = "flexible";

        // ── Engagement metrics ────────────────────────────────────────────
        /// Total AI chat sessions the user has had
        public int TotalSessions { get; set; } = 0;
        /// Current streak: consecutive days with AI interaction
        public int CurrentStreakDays { get; set; } = 0;
        /// Longest streak ever achieved
        public int LongestStreakDays { get; set; } = 0;
        /// Last time the user interacted with the AI
        public DateTime? LastInteractionAt { get; set; }
        /// AI-computed engagement score 0–100
        public double EngagementScore { get; set; } = 0;

        // ── AI coaching memory (JSON blobs) ──────────────────────────────
        /// JSON array of recent AI recommendations shown to the user
        public string LastRecommendations { get; set; } = "[]";
        /// JSON map of subject → avg grade trend (for detecting improvement)
        public string GradeTrends { get; set; } = "{}";
        /// JSON array of completed learning milestones
        public string Milestones { get; set; } = "[]";
        /// JSON array of active study goals the AI is tracking
        public string ActiveGoals { get; set; } = "[]";

        // ── Doctor-specific ───────────────────────────────────────────────
        /// Teaching style detected: "lecture", "interactive", "problem-based"
        public string TeachingStyle { get; set; } = string.Empty;
        /// JSON array of content topics the doctor has created exams/material for
        public string ContentHistory { get; set; } = "[]";

        // ── Meta ──────────────────────────────────────────────────────────
        public DateTime ProfileUpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public SystemUser User { get; set; } = null!;
        public ICollection<LearningSession> LearningSessions { get; set; } = new List<LearningSession>();
        public ICollection<AiInsight> Insights { get; set; } = new List<AiInsight>();
    }
}
