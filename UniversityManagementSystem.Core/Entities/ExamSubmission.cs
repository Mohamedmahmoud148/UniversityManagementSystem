using System;
using NUlid;

namespace UniversityManagementSystem.Core.Entities
{
    public class ExamSubmission : BaseEntity
    {
        public double? Score { get; set; }
        public bool IsGraded { get; set; }
        public string AnswersJson { get; set; } = "[]";
        public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
        public bool IsCompleted { get; set; } = false;
        /// <summary>Auto-saved draft answers during exam — wiped on final submit.</summary>
        public string? DraftAnswersJson { get; set; }
        public DateTime? LastSavedAt { get; set; }

        /// <summary>JSON dict of { questionId: awardedScore } for per-question manual grading.</summary>
        public string? GradingJson { get; set; }

        public Ulid ExamId { get; set; }
        public Ulid StudentId { get; set; }

        // Navigation Properties
        public Exam Exam { get; set; } = null!;
        public Student Student { get; set; } = null!;
    }
}
