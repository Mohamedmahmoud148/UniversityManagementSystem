using System;
using System.Collections.Generic;
// Re-trigger IDE analysis

namespace UniversityManagementSystem.Core.Entities
{
    public enum ExamType
    {
        Quiz,
        Midterm,
        Final
    }

    public class Exam : BaseEntity
    {
        public string Title { get; set; } = string.Empty;
        public ExamType Type { get; set; }
        public int TotalMarks { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public bool IsPublished { get; set; }

        public int SubjectOfferingId { get; set; }

        // Navigation Properties
        public SubjectOffering SubjectOffering { get; set; } = null!;
        public ICollection<ExamQuestion> Questions { get; set; } = new List<ExamQuestion>();
        public ICollection<ExamSubmission> Submissions { get; set; } = new List<ExamSubmission>();

        public void Update(string title, ExamType type, DateTime startTime, DateTime endTime, bool isPublished)
        {
            if (string.IsNullOrWhiteSpace(title)) throw new ArgumentNullException(nameof(title));
            if (endTime <= startTime) throw new ArgumentException("EndTime must be after StartTime");

            Title = title;
            Type = type;
            StartTime = startTime;
            EndTime = endTime;
            IsPublished = isPublished;
        }
    }
}
