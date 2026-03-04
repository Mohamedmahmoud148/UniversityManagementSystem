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
        public string PublicId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public ExamType Type { get; set; }
        public int TotalMarks { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public ExamMode Mode { get; set; } = ExamMode.Structured;
        public ExamStatus Status { get; set; } = ExamStatus.Draft;
        public string? FilePath { get; set; }
        public int CreatedByDoctorId { get; set; }

        public int SubjectOfferingId { get; set; }

        // Navigation Properties
        public Doctor CreatedByDoctor { get; set; } = null!;
        public SubjectOffering SubjectOffering { get; set; } = null!;
        public ICollection<ExamQuestion> Questions { get; set; } = new List<ExamQuestion>();
        public ICollection<ExamSubmission> Submissions { get; set; } = new List<ExamSubmission>();

        public void Update(string title, ExamType type, DateTime startTime, DateTime endTime, ExamStatus status, ExamMode mode, string? filePath)
        {
            if (string.IsNullOrWhiteSpace(title)) throw new ArgumentNullException(nameof(title));
            if (endTime <= startTime) throw new ArgumentException("EndTime must be after StartTime");

            Title = title;
            Type = type;
            StartTime = startTime;
            EndTime = endTime;
            Status = status;
            Mode = mode;
            FilePath = filePath;
        }
    }
}
