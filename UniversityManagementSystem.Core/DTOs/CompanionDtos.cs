using System;
using System.Collections.Generic;
using UniversityManagementSystem.Core.Entities;

namespace UniversityManagementSystem.Core.DTOs.Companion
{
    // ── Profile ───────────────────────────────────────────────────────────────

    public record AiCompanionProfileDto(
        string UserId,
        string LearningStyle,
        string CurrentGoal,
        string PreferredStudyTime,
        List<string> WeakSubjects,
        List<string> StrongSubjects,
        int TotalSessions,
        int CurrentStreakDays,
        int LongestStreakDays,
        double EngagementScore,
        DateTime? LastInteractionAt,
        List<string> ActiveGoals,
        List<string> Milestones);

    public record UpdateCompanionProfileDto(
        string? LearningStyle,
        string? CurrentGoal,
        string? PreferredStudyTime);

    // ── Learning Session ──────────────────────────────────────────────────────

    public record StartLearningSessionDto(
        string SessionType,          // "quiz" | "flashcards" | "concept_review" | "exam_prep"
        string TopicName,
        string? SubjectOfferingId);

    public record CompleteSessionDto(
        int TotalQuestions,
        int CorrectAnswers,
        int DurationMinutes,
        string? SessionDataJson);    // Optional: Q&A pairs for storage

    public record LearningSessionDto(
        string Id,
        string SessionType,
        string TopicName,
        string Status,
        int TotalQuestions,
        int CorrectAnswers,
        double AccuracyPercent,
        int DurationMinutes,
        string AiFeedback,
        DateTime StartedAt,
        DateTime? CompletedAt);

    // ── Flashcards ────────────────────────────────────────────────────────────

    public record GenerateFlashcardsDto(
        string TopicName,
        string? SubjectOfferingId,
        int CardCount = 15,
        string Difficulty = "mixed");   // "easy" | "medium" | "hard" | "mixed"

    public record FlashcardDeckDto(
        string Id,
        string Title,
        string TopicName,
        int CardCount,
        int DueToday,
        DateTime CreatedAt,
        List<FlashcardDto> Cards);

    public record FlashcardDto(
        string Id,
        string Front,
        string Back,
        string? Hint,
        string Difficulty,
        int RepetitionCount,
        double EaseFactor,
        DateTime NextReviewAt);

    public record ReviewFlashcardDto(
        int Quality);   // SM-2 quality: 0=blackout, 1=wrong, 2=wrong+hint, 3=correct+hard, 4=correct, 5=perfect

    // ── Insights ──────────────────────────────────────────────────────────────

    public record AiInsightDto(
        string Id,
        string InsightType,
        string Priority,
        string Title,
        string Message,
        string? ActionUrl,
        bool IsAcknowledged,
        DateTime CreatedAt,
        DateTime? ExpiresAt);

    // ── Dashboard ─────────────────────────────────────────────────────────────

    public record CompanionDashboardDto(
        AiCompanionProfileDto Profile,
        List<AiInsightDto> RecentInsights,
        List<FlashcardDto> DueFlashcards,
        WeeklyProgressDto WeeklyProgress,
        List<string> TodayRecommendations);

    public record WeeklyProgressDto(
        int SessionsThisWeek,
        double AverageAccuracy,
        int FlashcardsReviewed,
        int StudyMinutes,
        List<DailyActivityDto> DailyActivity);

    public record DailyActivityDto(
        string Date,
        int Sessions,
        int Minutes);

    // ── Doctor Analytics ─────────────────────────────────────────────────────

    public record ClassAnalyticsDto(
        string SubjectOfferingId,
        string SubjectName,
        int TotalStudents,
        double AverageGrade,
        double PassRate,
        double AtRiskPercent,
        List<WeakTopicDto> WeakTopics,
        List<PerformanceTrendDto> GradeTrend,
        string AiSummary);

    public record WeakTopicDto(
        string TopicName,
        double AverageScore,
        int StudentsStruggling,
        string AiRecommendation);

    public record PerformanceTrendDto(
        string Label,
        double Average,
        double PassRate);
}
