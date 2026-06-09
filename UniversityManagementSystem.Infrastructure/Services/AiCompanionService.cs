using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NUlid;
using UniversityManagementSystem.Core.DTOs.Companion;
using UniversityManagementSystem.Core.Entities;
using UniversityManagementSystem.Core.Interfaces;
using UniversityManagementSystem.Infrastructure.Data;

namespace UniversityManagementSystem.Infrastructure.Services
{
    public class AiCompanionService(
        AppDbContext context,
        IAiService aiService,
        ILogger<AiCompanionService> logger) : IAiCompanionService
    {
        private readonly AppDbContext _context = context;
        private readonly IAiService _aiService = aiService;
        private readonly ILogger<AiCompanionService> _logger = logger;

        // ── Profile ───────────────────────────────────────────────────────

        public async Task<AiCompanionProfileDto> GetOrCreateProfileAsync(Ulid userId)
        {
            var profile = await _context.AiCompanionProfiles
                .FirstOrDefaultAsync(p => p.UserId == userId);

            if (profile == null)
            {
                profile = new AiCompanionProfile
                {
                    UserId = userId,
                    ProfileUpdatedAt = DateTime.UtcNow,
                };
                _context.AiCompanionProfiles.Add(profile);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Created AiCompanionProfile for user {UserId}", userId);
            }

            return MapToProfileDto(profile);
        }

        public async Task<AiCompanionProfileDto> UpdateProfileAsync(
            Ulid userId, UpdateCompanionProfileDto dto)
        {
            var profile = await _context.AiCompanionProfiles
                .FirstOrDefaultAsync(p => p.UserId == userId)
                ?? throw new KeyNotFoundException("Profile not found.");

            if (dto.LearningStyle is not null)
                profile.LearningStyle = dto.LearningStyle;
            if (dto.CurrentGoal is not null)
                profile.CurrentGoal = dto.CurrentGoal;
            if (dto.PreferredStudyTime is not null)
                profile.PreferredStudyTime = dto.PreferredStudyTime;

            profile.ProfileUpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return MapToProfileDto(profile);
        }

        // ── Dashboard ─────────────────────────────────────────────────────

        public async Task<CompanionDashboardDto> GetDashboardAsync(Ulid userId)
        {
            var profile = await GetOrCreateProfileAsync(userId);

            var insights = await GetMyInsightsAsync(userId, unreadOnly: true);
            var dueCards = await GetDueCardsAsync(userId, limit: 5);
            var weeklyProgress = await BuildWeeklyProgressAsync(userId);

            // AI-generated today's recommendations (fast: from profile data, no LLM)
            var recommendations = BuildTodayRecommendations(profile);

            return new CompanionDashboardDto(
                Profile: profile,
                RecentInsights: insights.Take(5).ToList(),
                DueFlashcards: dueCards.ToList(),
                WeeklyProgress: weeklyProgress,
                TodayRecommendations: recommendations);
        }

        // ── Learning Sessions ─────────────────────────────────────────────

        public async Task<LearningSessionDto> StartSessionAsync(
            Ulid userId, StartLearningSessionDto dto)
        {
            var profile = await _context.AiCompanionProfiles
                .FirstOrDefaultAsync(p => p.UserId == userId);

            if (profile == null)
            {
                profile = new AiCompanionProfile { UserId = userId };
                _context.AiCompanionProfiles.Add(profile);
                await _context.SaveChangesAsync();
            }

            var sessionType = Enum.TryParse<LearningSessionType>(
                dto.SessionType, ignoreCase: true, out var st)
                ? st : LearningSessionType.FreeStudy;

            Ulid? offeringId = null;
            if (Ulid.TryParse(dto.SubjectOfferingId, out var oid))
                offeringId = oid;

            var initialMeta = JsonSerializer.Serialize(new
            {
                difficulty     = dto.Difficulty ?? "medium",
                question_count = dto.QuestionCount > 0 ? dto.QuestionCount : 5,
                questions      = Array.Empty<object>(),
                answers        = new { }
            });

            var session = new LearningSession
            {
                UserId = userId,
                AiCompanionProfileId = profile.Id,
                SubjectOfferingId = offeringId,
                SessionType = sessionType,
                TopicName = dto.TopicName,
                Status = LearningSessionStatus.Active,
                StartedAt = DateTime.UtcNow,
                SessionData = initialMeta,
            };

            _context.LearningSessions.Add(session);
            profile.TotalSessions++;
            profile.LastInteractionAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return MapToSessionDto(session);
        }

        public async Task<LearningSessionDto> CompleteSessionAsync(
            Ulid sessionId, CompleteSessionDto dto)
        {
            var session = await _context.LearningSessions
                .Include(s => s.AiCompanionProfile)
                .FirstOrDefaultAsync(s => s.Id == sessionId)
                ?? throw new KeyNotFoundException("Session not found.");

            session.Status = LearningSessionStatus.Completed;
            session.TotalQuestions = dto.TotalQuestions;
            session.CorrectAnswers = dto.CorrectAnswers;
            session.DurationMinutes = dto.DurationMinutes;
            session.CompletedAt = DateTime.UtcNow;
            session.AccuracyPercent = dto.TotalQuestions > 0
                ? Math.Round((double)dto.CorrectAnswers / dto.TotalQuestions * 100, 1)
                : 0;

            if (!string.IsNullOrEmpty(dto.SessionDataJson))
                session.SessionData = dto.SessionDataJson;

            // Generate AI feedback for the session
            session.AiFeedback = await GenerateSessionFeedbackAsync(session);

            // Update streak and engagement score
            UpdateEngagementMetrics(session.AiCompanionProfile, session);

            await _context.SaveChangesAsync();
            return MapToSessionDto(session);
        }

        public async Task<IList<LearningSessionDto>> GetSessionHistoryAsync(
            Ulid userId, int page = 1, int pageSize = 20)
        {
            var sessions = await _context.LearningSessions
                .Where(s => s.UserId == userId && s.DeletedAt == null)
                .OrderByDescending(s => s.StartedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return sessions.Select(MapToSessionDto).ToList();
        }

        // ── Interactive Session ───────────────────────────────────────────

        public async Task<List<SessionQuestionDto>> GenerateSessionQuestionsAsync(Ulid sessionId)
        {
            var session = await _context.LearningSessions
                .FirstOrDefaultAsync(s => s.Id == sessionId)
                ?? throw new KeyNotFoundException("Session not found.");

            if (session.Status != LearningSessionStatus.Active)
                throw new InvalidOperationException("Session is not active.");

            var sessionMeta = ParseSessionMeta(session.SessionData);
            var difficulty = sessionMeta.TryGetValue("difficulty", out var d) ? d : "medium";
            var count = sessionMeta.TryGetValue("question_count", out var qc)
                && int.TryParse(qc, out var n) ? n : 5;

            var aiQuestions = await _aiService.GenerateStudyQuestionsAsync(
                session.TopicName,
                session.SessionType.ToString().ToLower(),
                difficulty,
                count);

            // store questions in SessionData so we can grade later
            var stored = new StoredSessionData
            {
                Difficulty = difficulty,
                QuestionCount = count,
                Questions = aiQuestions.Select(q => new StoredQuestion
                {
                    Id            = q.Id,
                    Type          = q.Type,
                    Text          = q.Text,
                    Options       = q.Options,
                    CorrectAnswer = q.CorrectAnswer,
                    Explanation   = q.Explanation,
                }).ToList()
            };
            session.SessionData = JsonSerializer.Serialize(stored);
            await _context.SaveChangesAsync();

            return stored.Questions.Select((q, i) => new SessionQuestionDto(
                Id:             q.Id,
                QuestionNumber: i + 1,
                QuestionType:   q.Type,
                Text:           q.Text,
                Options:        q.Options)).ToList();
        }

        public async Task<AnswerResultDto> SubmitAnswerAsync(Ulid sessionId, SubmitAnswerDto dto)
        {
            var session = await _context.LearningSessions
                .FirstOrDefaultAsync(s => s.Id == sessionId)
                ?? throw new KeyNotFoundException("Session not found.");

            if (session.Status != LearningSessionStatus.Active)
                throw new InvalidOperationException("Session is not active.");

            var stored = JsonSerializer.Deserialize<StoredSessionData>(session.SessionData ?? "{}")
                         ?? new StoredSessionData();

            var question = stored.Questions.FirstOrDefault(q => q.Id == dto.QuestionId)
                ?? throw new KeyNotFoundException("Question not found in this session.");

            if (stored.Answers.ContainsKey(dto.QuestionId))
                throw new InvalidOperationException("Question already answered.");

            bool isCorrect;
            double score;
            string feedback;
            string explanation = question.Explanation;

            if (question.Type == "mcq")
            {
                isCorrect = string.Equals(dto.Answer.Trim(), question.CorrectAnswer.Trim(),
                    StringComparison.OrdinalIgnoreCase);
                score     = isCorrect ? 100 : 0;
                feedback  = isCorrect
                    ? "أحسنت! إجابة صحيحة ✅"
                    : $"إجابة خاطئة. الإجابة الصحيحة هي: {question.CorrectAnswer}";
            }
            else
            {
                var gradeResult = await _aiService.GradeOpenAnswerAsync(
                    question.Text, dto.Answer,
                    session.TopicName,
                    stored.Difficulty);

                if (gradeResult != null)
                {
                    isCorrect   = gradeResult.IsCorrect;
                    score       = gradeResult.Score;
                    feedback    = gradeResult.Feedback;
                    explanation = gradeResult.Explanation.Length > 0 ? gradeResult.Explanation : explanation;
                }
                else
                {
                    // AI unavailable — basic keyword check fallback
                    isCorrect = dto.Answer.Length > 10;
                    score     = isCorrect ? 60 : 0;
                    feedback  = "تم تسجيل إجابتك. سيتم مراجعتها.";
                }
            }

            stored.Answers[dto.QuestionId] = new StoredAnswer
            {
                StudentAnswer = dto.Answer,
                IsCorrect     = isCorrect,
                Score         = score,
            };

            session.SessionData = JsonSerializer.Serialize(stored);
            await _context.SaveChangesAsync();

            return new AnswerResultDto(
                QuestionId:    dto.QuestionId,
                IsCorrect:     isCorrect,
                Score:         score,
                CorrectAnswer: question.CorrectAnswer,
                Explanation:   explanation,
                AiFeedback:    feedback);
        }

        public async Task<SessionReportDto> GetSessionReportAsync(Ulid sessionId)
        {
            var session = await _context.LearningSessions
                .FirstOrDefaultAsync(s => s.Id == sessionId)
                ?? throw new KeyNotFoundException("Session not found.");

            var stored = JsonSerializer.Deserialize<StoredSessionData>(session.SessionData ?? "{}")
                         ?? new StoredSessionData();

            var answered = stored.Questions
                .Where(q => stored.Answers.ContainsKey(q.Id))
                .ToList();

            int total   = stored.Questions.Count;
            int correct = stored.Answers.Values.Count(a => a.IsCorrect);
            double accuracy = total > 0 ? Math.Round((double)correct / total * 100, 1) : 0;

            // auto-complete session if not already done
            if (session.Status == LearningSessionStatus.Active)
            {
                session.Status          = LearningSessionStatus.Completed;
                session.TotalQuestions  = total;
                session.CorrectAnswers  = correct;
                session.AccuracyPercent = accuracy;
                session.CompletedAt     = DateTime.UtcNow;
                session.DurationMinutes = (int)(DateTime.UtcNow - session.StartedAt).TotalMinutes;

                session.AiFeedback = await GenerateSessionFeedbackAsync(session);

                if (session.AiCompanionProfile == null)
                    await _context.Entry(session).Reference(s => s.AiCompanionProfile).LoadAsync();
                if (session.AiCompanionProfile != null)
                    UpdateEngagementMetrics(session.AiCompanionProfile, session);

                await _context.SaveChangesAsync();
            }

            string level = accuracy switch
            {
                >= 90 => "Excellent",
                >= 75 => "Good",
                >= 50 => "Needs Improvement",
                _     => "Poor"
            };

            var recommendations = BuildSessionRecommendations(accuracy, stored.Questions, stored.Answers);

            var review = stored.Questions.Select((q, i) =>
            {
                stored.Answers.TryGetValue(q.Id, out var ans);
                return new QuestionReviewDto(
                    QuestionNumber: i + 1,
                    QuestionText:   q.Text,
                    StudentAnswer:  ans?.StudentAnswer ?? "لم يُجب",
                    CorrectAnswer:  q.CorrectAnswer,
                    IsCorrect:      ans?.IsCorrect ?? false,
                    Explanation:    q.Explanation);
            }).ToList();

            return new SessionReportDto(
                SessionId:        sessionId.ToString(),
                TopicName:        session.TopicName,
                SessionType:      session.SessionType.ToString(),
                Difficulty:       stored.Difficulty,
                TotalQuestions:   total,
                CorrectAnswers:   correct,
                AccuracyPercent:  accuracy,
                DurationMinutes:  session.DurationMinutes,
                PerformanceLevel: level,
                OverallFeedback:  session.AiFeedback ?? string.Empty,
                QuestionReview:   review,
                Recommendations:  recommendations);
        }

        // ── Internal helpers ──────────────────────────────────────────────

        private static Dictionary<string, string> ParseSessionMeta(string? json)
        {
            if (string.IsNullOrEmpty(json)) return new();
            try
            {
                var doc = JsonDocument.Parse(json);
                var dict = new Dictionary<string, string>();
                foreach (var prop in doc.RootElement.EnumerateObject())
                    dict[prop.Name] = prop.Value.ToString();
                return dict;
            }
            catch { return new(); }
        }

        private static List<string> BuildSessionRecommendations(
            double accuracy, List<StoredQuestion> questions, Dictionary<string, StoredAnswer> answers)
        {
            var recs = new List<string>();
            var wrong = questions.Where(q =>
                !answers.TryGetValue(q.Id, out var a) || !a.IsCorrect).ToList();

            if (accuracy >= 90)
                recs.Add("ممتاز! جرب مستوى صعوبة أعلى في الجلسة القادمة.");
            else if (accuracy >= 75)
                recs.Add("أداء جيد! راجع الأسئلة اللي غلطت فيها مرة تانية.");
            else
                recs.Add("نصيحة: اذاكر الموضوع ده تاني وارجع لجلسة جديدة.");

            if (wrong.Count > 0)
                recs.Add($"ركز على هذه النقاط: {string.Join("، ", wrong.Take(3).Select(q => q.Text.Length > 40 ? q.Text[..40] + "..." : q.Text))}");

            return recs;
        }

        // ── Stored session data model (JSON in SessionData column) ────────

        private class StoredSessionData
        {
            [JsonPropertyName("difficulty")]     public string Difficulty   { get; set; } = "medium";
            [JsonPropertyName("question_count")] public int QuestionCount   { get; set; } = 5;
            [JsonPropertyName("questions")]      public List<StoredQuestion> Questions { get; set; } = [];
            [JsonPropertyName("answers")]        public Dictionary<string, StoredAnswer> Answers { get; set; } = [];
        }

        private class StoredQuestion
        {
            [JsonPropertyName("id")]             public string Id           { get; set; } = string.Empty;
            [JsonPropertyName("type")]           public string Type         { get; set; } = "mcq";
            [JsonPropertyName("text")]           public string Text         { get; set; } = string.Empty;
            [JsonPropertyName("options")]        public List<string>? Options { get; set; }
            [JsonPropertyName("correct_answer")] public string CorrectAnswer { get; set; } = string.Empty;
            [JsonPropertyName("explanation")]    public string Explanation  { get; set; } = string.Empty;
        }

        private class StoredAnswer
        {
            [JsonPropertyName("student_answer")] public string StudentAnswer { get; set; } = string.Empty;
            [JsonPropertyName("is_correct")]     public bool IsCorrect       { get; set; }
            [JsonPropertyName("score")]          public double Score         { get; set; }
        }

        // ── Flashcards ────────────────────────────────────────────────────

        public async Task<FlashcardDeckDto> GenerateFlashcardsAsync(
            Ulid userId, GenerateFlashcardsDto dto)
        {
            Ulid? offeringId = null;
            if (Ulid.TryParse(dto.SubjectOfferingId, out var oid))
                offeringId = oid;

            // Call FastAPI AI service to generate flashcard content
            var aiCards = await _aiService.GenerateFlashcardsAsync(
                dto.TopicName, dto.CardCount, dto.Difficulty);

            var deck = new FlashcardDeck
            {
                UserId = userId,
                SubjectOfferingId = offeringId,
                Title = $"Flashcards: {dto.TopicName}",
                TopicName = dto.TopicName,
                CardCount = aiCards.Count,
            };
            _context.FlashcardDecks.Add(deck);
            await _context.SaveChangesAsync();

            var cards = aiCards.Select(c => new Flashcard
            {
                DeckId = deck.Id,
                Front = c.Front,
                Back = c.Back,
                Hint = c.Hint,
                Difficulty = Enum.TryParse<FlashcardDifficulty>(
                    c.Difficulty, ignoreCase: true, out var d) ? d : FlashcardDifficulty.Medium,
                NextReviewAt = DateTime.UtcNow,
            }).ToList();

            _context.Flashcards.AddRange(cards);
            await _context.SaveChangesAsync();

            return await GetDeckAsync(deck.Id);
        }

        public async Task<IList<FlashcardDeckDto>> GetMyDecksAsync(Ulid userId)
        {
            var decks = await _context.FlashcardDecks
                .Where(d => d.UserId == userId && d.DeletedAt == null)
                .Include(d => d.Cards)
                .OrderByDescending(d => d.CreatedAt)
                .ToListAsync();

            return decks.Select(d => MapToDeckDto(d, userId)).ToList();
        }

        public async Task<FlashcardDeckDto> GetDeckAsync(Ulid deckId)
        {
            var deck = await _context.FlashcardDecks
                .Include(d => d.Cards)
                .FirstOrDefaultAsync(d => d.Id == deckId && d.DeletedAt == null)
                ?? throw new KeyNotFoundException("Flashcard deck not found.");

            return MapToDeckDto(deck, deck.UserId);
        }

        public async Task<FlashcardDto> ReviewCardAsync(Ulid cardId, ReviewFlashcardDto dto)
        {
            var card = await _context.Flashcards
                .FirstOrDefaultAsync(c => c.Id == cardId)
                ?? throw new KeyNotFoundException("Flashcard not found.");

            // SM-2 Algorithm implementation
            ApplySm2(card, dto.Quality);

            card.LastReviewedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return MapToCardDto(card);
        }

        public async Task<IList<FlashcardDto>> GetDueCardsAsync(Ulid userId, int limit = 20)
        {
            var now = DateTime.UtcNow;
            var cards = await _context.Flashcards
                .Include(c => c.Deck)
                .Where(c => c.Deck.UserId == userId
                         && c.Deck.DeletedAt == null
                         && c.NextReviewAt <= now)
                .OrderBy(c => c.NextReviewAt)
                .Take(limit)
                .ToListAsync();

            return cards.Select(MapToCardDto).ToList();
        }

        // ── Insights ──────────────────────────────────────────────────────

        public async Task<IList<AiInsightDto>> GetMyInsightsAsync(
            Ulid userId, bool unreadOnly = false)
        {
            var query = _context.AiInsights
                .Where(i => i.UserId == userId && i.DeletedAt == null
                    && (i.ExpiresAt == null || i.ExpiresAt > DateTime.UtcNow));

            if (unreadOnly)
                query = query.Where(i => !i.IsAcknowledged);

            var insights = await query
                .OrderByDescending(i => i.CreatedAt)
                .Take(50)
                .ToListAsync();

            return insights.Select(MapToInsightDto).ToList();
        }

        public async Task AcknowledgeInsightAsync(Ulid insightId, Ulid userId)
        {
            var insight = await _context.AiInsights
                .FirstOrDefaultAsync(i => i.Id == insightId)
                ?? throw new KeyNotFoundException("Insight not found.");

            if (insight.UserId != userId)
                throw new UnauthorizedAccessException("Not your insight.");

            insight.IsAcknowledged = true;
            await _context.SaveChangesAsync();
        }

        // ── Doctor Analytics ──────────────────────────────────────────────

        public async Task<ClassAnalyticsDto> GetClassAnalyticsAsync(
            Ulid subjectOfferingId, Ulid doctorUserId)
        {
            // Verify the doctor owns this offering
            var doctorProfile = await _context.Doctors
                .FirstOrDefaultAsync(d => d.SystemUserId == doctorUserId);

            var offering = await _context.SubjectOfferings
                .Include(o => o.Subject)
                .FirstOrDefaultAsync(o => o.Id == subjectOfferingId)
                ?? throw new KeyNotFoundException("Subject offering not found.");

            if (doctorProfile != null && offering.DoctorId != doctorProfile.Id)
                throw new UnauthorizedAccessException("You are not assigned to this subject.");

            // Aggregate grade data
            var grades = await _context.StudentGrades
                .Where(g => g.SubjectOfferingId == subjectOfferingId)
                .Select(g => new { g.FinalScore, g.StudentId })
                .ToListAsync();

            var totalStudents = grades.Count;
            var avgGrade = totalStudents > 0 ? grades.Average(g => g.FinalScore) : 0;
            var passCount = grades.Count(g => g.FinalScore >= 50);
            var passRate = totalStudents > 0 ? (double)passCount / totalStudents * 100 : 0;
            var atRisk = grades.Count(g => g.FinalScore < 50);
            var atRiskPct = totalStudents > 0 ? (double)atRisk / totalStudents * 100 : 0;

            // Build grade trend from exam results
            var trend = await BuildGradeTrendAsync(subjectOfferingId);
            var weakTopics = await GetClassWeakTopicsAsync(subjectOfferingId, doctorUserId);

            // Generate AI summary
            var aiSummary = await GenerateClassSummaryAsync(
                offering.Subject?.Name ?? "Subject", totalStudents, avgGrade,
                passRate, atRiskPct, weakTopics);

            return new ClassAnalyticsDto(
                SubjectOfferingId: subjectOfferingId.ToString(),
                SubjectName: offering.Subject?.Name ?? "Unknown",
                TotalStudents: totalStudents,
                AverageGrade: Math.Round(avgGrade, 1),
                PassRate: Math.Round(passRate, 1),
                AtRiskPercent: Math.Round(atRiskPct, 1),
                WeakTopics: weakTopics.ToList(),
                GradeTrend: trend,
                AiSummary: aiSummary);
        }

        public async Task<IList<WeakTopicDto>> GetClassWeakTopicsAsync(
            Ulid subjectOfferingId, Ulid doctorUserId)
        {
            // Derive weak topics from exam-level scores (AnswersJson is a blob, not navigable)
            var examStats = await _context.Exams
                .Where(e => e.SubjectOfferingId == subjectOfferingId)
                .Select(e => new
                {
                    e.Title,
                    e.TotalMarks,
                    SubmissionCount = e.Submissions.Count(s => s.Score.HasValue),
                    AvgScore = e.Submissions.Any(s => s.Score.HasValue)
                        ? e.Submissions.Where(s => s.Score.HasValue).Average(s => (double)s.Score!.Value) : 0.0,
                    FailCount = e.Submissions.Count(s => s.Score.HasValue && s.Score < e.TotalMarks * 0.5),
                })
                .Where(e => e.SubmissionCount >= 2)
                .OrderBy(e => e.AvgScore)
                .Take(10)
                .ToListAsync();

            var weakTopics = new List<WeakTopicDto>();
            foreach (var stat in examStats)
            {
                double avgPercent = stat.TotalMarks > 0
                    ? Math.Round(stat.AvgScore / stat.TotalMarks * 100, 1) : 0;
                if (avgPercent >= 65) continue; // not weak enough
                var rec = await GenerateTopicRecommendationAsync(stat.Title, avgPercent);
                weakTopics.Add(new WeakTopicDto(
                    TopicName: stat.Title,
                    AverageScore: avgPercent,
                    StudentsStruggling: stat.FailCount,
                    AiRecommendation: rec));
            }
            return weakTopics;
        }

        // ── Private helpers ───────────────────────────────────────────────

        private static void ApplySm2(Flashcard card, int quality)
        {
            // SM-2 spaced repetition algorithm
            quality = Math.Clamp(quality, 0, 5);

            if (quality < 3)
            {
                // Incorrect — reset repetition
                card.RepetitionCount = 0;
                card.IntervalDays = 1;
            }
            else
            {
                // Correct
                card.IntervalDays = card.RepetitionCount switch
                {
                    0 => 1,
                    1 => 6,
                    _ => (int)Math.Round(card.IntervalDays * card.EaseFactor),
                };
                card.RepetitionCount++;
            }

            // Update ease factor (SM-2 formula)
            card.EaseFactor = Math.Max(1.3,
                card.EaseFactor + 0.1 - (5 - quality) * (0.08 + (5 - quality) * 0.02));
            card.NextReviewAt = DateTime.UtcNow.AddDays(card.IntervalDays);
        }

        private static void UpdateEngagementMetrics(
            AiCompanionProfile profile, LearningSession session)
        {
            var now = DateTime.UtcNow;
            var lastDay = profile.LastInteractionAt?.Date;

            if (lastDay == now.Date.AddDays(-1))
                profile.CurrentStreakDays++;
            else if (lastDay != now.Date)
                profile.CurrentStreakDays = 1;  // restart streak

            if (profile.CurrentStreakDays > profile.LongestStreakDays)
                profile.LongestStreakDays = profile.CurrentStreakDays;

            profile.LastInteractionAt = now;

            // Simple engagement score: weighted average
            var accuracyBonus = session.AccuracyPercent * 0.3;
            var durationBonus = Math.Min(session.DurationMinutes * 0.5, 20);
            var streakBonus = Math.Min(profile.CurrentStreakDays * 2.0, 30);
            profile.EngagementScore = Math.Min(100,
                profile.EngagementScore * 0.8 + (accuracyBonus + durationBonus + streakBonus) * 0.2);
        }

        private async Task<string> GenerateSessionFeedbackAsync(LearningSession session)
        {
            try
            {
                var prompt = $"Generate brief encouraging feedback (2-3 sentences) for a student who just completed a {session.SessionType} session on '{session.TopicName}'. They answered {session.CorrectAnswers}/{session.TotalQuestions} questions correctly ({session.AccuracyPercent:F0}% accuracy) in {session.DurationMinutes} minutes. Match the student's language — if the topic sounds Arabic, respond in Arabic.";
                var result = await _aiService.SendQuickPromptAsync(prompt);
                return result ?? "Great work on your study session! Keep it up.";
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Session feedback generation failed: {Error}", ex.Message);
                return session.AccuracyPercent >= 80
                    ? "ممتاز! أداء رائع في الجلسة دي. 🎉"
                    : "استمر في المذاكرة! المراجعة المنتظمة هتحسن أداءك. 💪";
            }
        }

        private async Task<string> GenerateClassSummaryAsync(
            string subjectName, int total, double avg, double passRate,
            double atRisk, IList<WeakTopicDto> weakTopics)
        {
            try
            {
                var topicList = string.Join(", ", weakTopics.Take(3).Select(t => t.TopicName));
                var prompt = $"Analyze class performance: Subject={subjectName}, Students={total}, Average={avg:F1}, PassRate={passRate:F0}%, AtRisk={atRisk:F0}%, WeakTopics={topicList}. Provide a 2-3 sentence academic insight for the doctor. Respond in Arabic.";
                return await _aiService.SendQuickPromptAsync(prompt) ?? "بيانات الفصل تم تحليلها.";
            }
            catch { return "تم تحليل بيانات الفصل الدراسي."; }
        }

        private async Task<string> GenerateTopicRecommendationAsync(string topic, double avgScore)
        {
            try
            {
                var prompt = $"Topic '{topic}' has average score {avgScore:F0}%. Give a 1-sentence teaching recommendation for the doctor. Be specific. Respond in Arabic.";
                return await _aiService.SendQuickPromptAsync(prompt) ?? "يُنصح بمراجعة هذا الموضوع.";
            }
            catch { return "يُنصح بإعادة شرح هذا الموضوع بأمثلة تطبيقية."; }
        }

        private async Task<WeeklyProgressDto> BuildWeeklyProgressAsync(Ulid userId)
        {
            var weekAgo = DateTime.UtcNow.AddDays(-7);
            var sessions = await _context.LearningSessions
                .Where(s => s.UserId == userId
                         && s.StartedAt >= weekAgo
                         && s.Status == LearningSessionStatus.Completed)
                .ToListAsync();

            var dailyActivity = Enumerable.Range(0, 7)
                .Select(i =>
                {
                    var date = DateTime.UtcNow.AddDays(-i).Date;
                    var daySessions = sessions.Where(s => s.StartedAt.Date == date).ToList();
                    return new DailyActivityDto(
                        Date: date.ToString("yyyy-MM-dd"),
                        Sessions: daySessions.Count,
                        Minutes: daySessions.Sum(s => s.DurationMinutes));
                })
                .OrderBy(d => d.Date)
                .ToList();

            var flashcardsReviewed = await _context.Flashcards
                .Include(c => c.Deck)
                .CountAsync(c => c.Deck.UserId == userId
                              && c.LastReviewedAt >= weekAgo);

            return new WeeklyProgressDto(
                SessionsThisWeek: sessions.Count,
                AverageAccuracy: sessions.Any()
                    ? Math.Round(sessions.Average(s => s.AccuracyPercent), 1) : 0,
                FlashcardsReviewed: flashcardsReviewed,
                StudyMinutes: sessions.Sum(s => s.DurationMinutes),
                DailyActivity: dailyActivity);
        }

        private async Task<List<PerformanceTrendDto>> BuildGradeTrendAsync(Ulid subjectOfferingId)
        {
            var exams = await _context.Exams
                .Where(e => e.SubjectOfferingId == subjectOfferingId)
                .OrderBy(e => e.CreatedAt)
                .Take(6)
                .Select(e => new
                {
                    e.Title,
                    Submissions = e.Submissions.Where(s => s.Score != null)
                })
                .ToListAsync();

            return exams.Select(e => new PerformanceTrendDto(
                Label: e.Title,
                Average: e.Submissions.Any()
                    ? Math.Round(e.Submissions.Average(s => s.Score ?? 0), 1) : 0,
                PassRate: e.Submissions.Any()
                    ? Math.Round(e.Submissions.Count(s => (s.Score ?? 0) >= 50.0) * 100.0 / e.Submissions.Count(), 1) : 0
            )).ToList();
        }

        private static List<string> BuildTodayRecommendations(AiCompanionProfileDto profile)
        {
            var recs = new List<string>();

            if (profile.CurrentStreakDays == 0)
                recs.Add("ابدأ جلسة مذاكرة اليوم لتبدأ streak جديدة 🔥");

            if (profile.WeakSubjects.Any())
                recs.Add($"راجع موضوع في {profile.WeakSubjects.First()} — ده من أضعف مواضيعك 📚");

            if (profile.CurrentStreakDays > 0)
                recs.Add($"Streak بتاعك {profile.CurrentStreakDays} يوم — استمر عشان ما تكسرهاش ⚡");

            if (!recs.Any())
                recs.Add("جرب تعمل quiz سريع على أي موضوع بتذاكره دلوقتي 🎯");

            return recs.Take(3).ToList();
        }

        // ── Mapping helpers ───────────────────────────────────────────────

        private static AiCompanionProfileDto MapToProfileDto(AiCompanionProfile p)
        {
            var weakSubjects = TryParseStringList(p.WeakSubjects);
            var strongSubjects = TryParseStringList(p.StrongSubjects);
            var activeGoals = TryParseStringList(p.ActiveGoals);
            var milestones = TryParseStringList(p.Milestones);

            return new AiCompanionProfileDto(
                UserId: p.UserId.ToString(),
                LearningStyle: p.LearningStyle,
                CurrentGoal: p.CurrentGoal,
                PreferredStudyTime: p.PreferredStudyTime,
                WeakSubjects: weakSubjects,
                StrongSubjects: strongSubjects,
                TotalSessions: p.TotalSessions,
                CurrentStreakDays: p.CurrentStreakDays,
                LongestStreakDays: p.LongestStreakDays,
                EngagementScore: p.EngagementScore,
                LastInteractionAt: p.LastInteractionAt,
                ActiveGoals: activeGoals,
                Milestones: milestones);
        }

        private static LearningSessionDto MapToSessionDto(LearningSession s) =>
            new(s.Id.ToString(), s.SessionType.ToString(), s.TopicName,
                s.Status.ToString(), s.TotalQuestions, s.CorrectAnswers,
                s.AccuracyPercent, s.DurationMinutes, s.AiFeedback,
                s.StartedAt, s.CompletedAt);

        private static FlashcardDeckDto MapToDeckDto(FlashcardDeck deck, Ulid userId)
        {
            var now = DateTime.UtcNow;
            var dueToday = deck.Cards.Count(c => c.NextReviewAt <= now);
            return new FlashcardDeckDto(
                Id: deck.Id.ToString(),
                Title: deck.Title,
                TopicName: deck.TopicName,
                CardCount: deck.CardCount,
                DueToday: dueToday,
                CreatedAt: deck.CreatedAt,
                Cards: deck.Cards.Select(MapToCardDto).ToList());
        }

        private static FlashcardDto MapToCardDto(Flashcard c) =>
            new(c.Id.ToString(), c.Front, c.Back, c.Hint,
                c.Difficulty.ToString(), c.RepetitionCount,
                c.EaseFactor, c.NextReviewAt);

        private static AiInsightDto MapToInsightDto(AiInsight i) =>
            new(i.Id.ToString(), i.InsightType.ToString(),
                i.Priority.ToString(), i.Title, i.Message,
                i.ActionUrl, i.IsAcknowledged, i.CreatedAt, i.ExpiresAt);

        private static List<string> TryParseStringList(string json)
        {
            try { return JsonSerializer.Deserialize<List<string>>(json) ?? []; }
            catch { return []; }
        }
    }
}
