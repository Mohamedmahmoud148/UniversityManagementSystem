using NUlid;

namespace UniversityManagementSystem.Core.Entities
{
    /// <summary>
    /// Stores the specific subset (and order) of questions assigned to one student
    /// for a randomized exam. Created lazily when the student first opens the exam.
    /// </summary>
    public class StudentExamVariant : BaseEntity
    {
        public Ulid ExamId { get; set; }
        public Ulid StudentId { get; set; }

        /// <summary>JSON array of ExamQuestion ULIDs in the order the student should see them.</summary>
        public string QuestionIdsJson { get; set; } = "[]";

        // Navigation
        public Exam Exam { get; set; } = null!;
        public Student Student { get; set; } = null!;
    }
}
