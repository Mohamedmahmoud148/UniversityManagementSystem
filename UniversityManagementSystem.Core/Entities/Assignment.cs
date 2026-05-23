using System;
using System.Collections.Generic;
using NUlid;

namespace UniversityManagementSystem.Core.Entities
{
    public class Assignment : BaseEntity
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public Ulid SubjectOfferingId { get; set; }
        public Ulid DoctorId { get; set; }
        public DateTime Deadline { get; set; }
        public double MaxGrade { get; set; } = 100;
        public bool AllowLateSubmission { get; set; } = false;
        public bool AiGradingEnabled { get; set; } = false;

        /// <summary>JSON rubric sent to the AI grader.</summary>
        public string? GradingRubric { get; set; }

        // Navigation properties
        public SubjectOffering SubjectOffering { get; set; } = null!;
        public Doctor Doctor { get; set; } = null!;
        public ICollection<AssignmentSubmission> Submissions { get; set; } = new List<AssignmentSubmission>();
    }
}
