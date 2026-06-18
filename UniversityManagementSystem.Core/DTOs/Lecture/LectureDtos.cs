using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using UniversityManagementSystem.Core.Entities;

namespace UniversityManagementSystem.Core.DTOs.Lecture
{
    public class LectureRecordingDto
    {
        public string Id { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string OriginalFileName { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public int? DurationSeconds { get; set; }
        public string Status { get; set; } = string.Empty;
        public int TranscriptChars { get; set; }
        public string? ErrorMessage { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ProcessedAt { get; set; }
    }

    public class LectureSummaryDto
    {
        public string RecordingId { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
        public List<string> KeyConcepts { get; set; } = new();
        public List<LectureTimelineSection> Timeline { get; set; } = new();
        public List<LectureSuggestedQuestion> SuggestedQuestions { get; set; } = new();
    }

    public class LectureTimelineSection
    {
        [JsonPropertyName("title")] public string Title { get; set; } = string.Empty;
        [JsonPropertyName("start")] public int Start { get; set; }
        [JsonPropertyName("end")]   public int End { get; set; }
    }

    public class LectureSuggestedQuestion
    {
        [JsonPropertyName("question")]   public string Question   { get; set; } = string.Empty;
        [JsonPropertyName("difficulty")] public string Difficulty { get; set; } = "Medium";
    }

    public class LectureFlashcardDto
    {
        public string Id { get; set; } = string.Empty;
        public string Front { get; set; } = string.Empty;
        public string Back { get; set; } = string.Empty;
    }

    public class LectureQuizDto
    {
        public string Id { get; set; } = string.Empty;
        public string Question { get; set; } = string.Empty;
        public string OptionA { get; set; } = string.Empty;
        public string OptionB { get; set; } = string.Empty;
        public string OptionC { get; set; } = string.Empty;
        public string OptionD { get; set; } = string.Empty;
        public string CorrectAnswer { get; set; } = string.Empty;
        public string Explanation { get; set; } = string.Empty;
    }

    public class LectureDashboardDto
    {
        public int TotalRecordings { get; set; }
        public int TotalStudyHours { get; set; }
        public int TotalFlashcards { get; set; }
        public int TotalQuizQuestions { get; set; }
        public List<LectureRecordingDto> RecentRecordings { get; set; } = new();
    }

    public class LectureAskRequestDto
    {
        public string Message { get; set; } = string.Empty;
    }

    // ── FastAPI response shapes ───────────────────────────────────────────────

    public class FastApiTranscribeResponse
    {
        [JsonPropertyName("transcript")]       public string Transcript      { get; set; } = string.Empty;
        [JsonPropertyName("duration_seconds")] public int?   DurationSeconds { get; set; }
        [JsonPropertyName("provider")]         public string Provider        { get; set; } = "whisper";
    }

    public class FastApiAnalyzeResponse
    {
        [JsonPropertyName("summary")]            public string Summary { get; set; } = string.Empty;
        [JsonPropertyName("key_concepts")]       public List<string> KeyConcepts { get; set; } = new();
        [JsonPropertyName("timeline")]           public List<LectureTimelineSection> Timeline { get; set; } = new();
        [JsonPropertyName("flashcards")]         public List<FastApiFlashcard> Flashcards { get; set; } = new();
        [JsonPropertyName("quiz")]               public List<FastApiQuizItem>  Quiz       { get; set; } = new();
        [JsonPropertyName("suggested_questions")]public List<LectureSuggestedQuestion> SuggestedQuestions { get; set; } = new();
    }

    public class FastApiFlashcard
    {
        [JsonPropertyName("front")] public string Front { get; set; } = string.Empty;
        [JsonPropertyName("back")]  public string Back  { get; set; } = string.Empty;
    }

    public class FastApiQuizItem
    {
        [JsonPropertyName("question")]       public string Question      { get; set; } = string.Empty;
        [JsonPropertyName("option_a")]       public string OptionA       { get; set; } = string.Empty;
        [JsonPropertyName("option_b")]       public string OptionB       { get; set; } = string.Empty;
        [JsonPropertyName("option_c")]       public string OptionC       { get; set; } = string.Empty;
        [JsonPropertyName("option_d")]       public string OptionD       { get; set; } = string.Empty;
        [JsonPropertyName("correct_answer")] public string CorrectAnswer { get; set; } = string.Empty;
        [JsonPropertyName("explanation")]    public string Explanation   { get; set; } = string.Empty;
    }

    public class FastApiAskResponse
    {
        [JsonPropertyName("answer")] public string Answer { get; set; } = string.Empty;
    }
}
