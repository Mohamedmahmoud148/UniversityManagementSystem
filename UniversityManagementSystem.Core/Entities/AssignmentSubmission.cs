using System;
using NUlid;

namespace UniversityManagementSystem.Core.Entities
{
    public enum SubmissionStatus
    {
        Submitted,
        UnderReview,
        Graded,
        Rejected
    }

    public class AssignmentSubmission : BaseEntity
    {
        public Ulid AssignmentId { get; set; }
        public Ulid StudentId { get; set; }

        public string? TextAnswer { get; set; }
        public string? FileUrl { get; set; }
        public string? StorageKey { get; set; }

        public DateTime SubmittedAt { get; set; }
        public bool IsLate { get; set; }
        public SubmissionStatus Status { get; set; } = SubmissionStatus.Submitted;

        // Grading
        public double? Grade { get; set; }
        public string? Feedback { get; set; }
        public string? AiFeedback { get; set; }
        public string? Strengths { get; set; }
        public string? Weaknesses { get; set; }
        public bool IsAiGraded { get; set; } = false;
        public bool IsHumanReviewed { get; set; } = false;
        public Ulid? ReviewedByDoctorId { get; set; }

        // Navigation properties
        public Assignment Assignment { get; set; } = null!;
        public Student Student { get; set; } = null!;
    }
}
