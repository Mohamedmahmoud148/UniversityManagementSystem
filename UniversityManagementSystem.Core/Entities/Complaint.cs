using NUlid;

namespace UniversityManagementSystem.Core.Entities
{
    /// <summary>
    /// A student complaint directed at a doctor/exam/subject offering.
    /// Indexed on (CreatedAt, SubjectOfferingId, TargetDoctorId) for fast retrieval.
    /// </summary>
    public class Complaint : BaseEntity
    {
        /// <summary>SystemUser ID of the student who submitted the complaint.</summary>
        public Ulid UserId { get; set; }

        /// <summary>What the complaint targets: "Doctor" | "Exam" | "Grade" | "Other".</summary>
        public string TargetType { get; set; } = string.Empty;

        /// <summary>
        /// The ID of the target entity (DoctorId, ExamId, GradeId, etc.)
        /// Optional — depends on TargetType.
        /// </summary>
        public string? TargetId { get; set; }

        /// <summary>Subject offering the complaint belongs to (nullable for general complaints).</summary>
        public Ulid? SubjectOfferingId { get; set; }

        /// <summary>The doctor associated with the offering — denormalised for fast filtering.</summary>
        public Ulid? TargetDoctorId { get; set; }

        /// <summary>The complaint body (max 2000 chars validated in DTO).</summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>Workflow status: "Pending" | "UnderReview" | "Resolved" | "Dismissed".</summary>
        public string Status { get; set; } = "Pending";

        /// <summary>Optional resolution note added by admin/doctor.</summary>
        public string? ResolutionNote { get; set; }

        // Navigation
        public SystemUser User { get; set; } = null!;
        public SubjectOffering? SubjectOffering { get; set; }
    }
}
