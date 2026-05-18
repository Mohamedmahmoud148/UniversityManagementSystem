using NUlid;

namespace UniversityManagementSystem.Core.Entities
{
    /// <summary>
    /// Configurable GPA-based credit hour limits and academic standing thresholds.
    /// One global record (DepartmentId = null) serves as the default.
    /// Department-specific records override the global one when present.
    /// </summary>
    public class AcademicPolicy : BaseEntity
    {
        /// <summary>null = global policy (applies to all departments).</summary>
        public Ulid? DepartmentId { get; set; }

        // ── Credit Hour Limits ──────────────────────────────────────────
        public int DefaultMaxHours    { get; set; } = 18;
        public int HonorMaxHours      { get; set; } = 21;   // GPA >= HonorGpaThreshold
        public int WarningMaxHours    { get; set; } = 12;   // GPA < WarningGpaThreshold
        public int ProbationMaxHours  { get; set; } = 9;    // GPA < ProbationGpaThreshold

        // ── GPA Thresholds ──────────────────────────────────────────────
        public double WarningGpaThreshold    { get; set; } = 2.0;
        public double ProbationGpaThreshold  { get; set; } = 1.5;
        public double HonorGpaThreshold      { get; set; } = 3.5;
        public double GraduationMinGpa       { get; set; } = 2.0;

        // Navigation
        public Department? Department { get; set; }
    }
}
