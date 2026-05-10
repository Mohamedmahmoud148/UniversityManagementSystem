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
        public Ulid StudentId { get; set; }

        public string Title { get; set; } = string.Empty;

        /// <summary>The complaint body (max 2000 chars validated in DTO).</summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>What the complaint targets: "doctor" | "department" | "administration" | "technical" | "subject".</summary>
        public string TargetType { get; set; } = string.Empty;

        /// <summary>The ID of the target entity.</summary>
        public string TargetId { get; set; } = string.Empty;

        /// <summary>Workflow status: "Pending" | "UnderReview" | "Resolved" | "Dismissed".</summary>
        public string Status { get; set; } = "Pending";

        /// <summary>Priority: "Normal" | "High" | "Critical".</summary>
        public string Priority { get; set; } = "Normal";

        /// <summary>Optional resolution note added by admin/doctor.</summary>
        public string? ResolutionNote { get; set; }

        // Navigation
        public SystemUser Student { get; set; } = null!;
        public ComplaintAnalysis? Analysis { get; set; }
    }
}
