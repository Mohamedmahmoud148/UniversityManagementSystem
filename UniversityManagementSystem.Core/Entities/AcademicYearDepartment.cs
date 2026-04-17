using NUlid;

namespace UniversityManagementSystem.Core.Entities
{
    /// <summary>
    /// Junction table that controls which departments are available
    /// for a given academic year. IsActive lets admins toggle availability
    /// without removing the mapping record.
    /// </summary>
    public class AcademicYearDepartment : BaseEntity
    {
        public Ulid AcademicYearId { get; set; }
        public Ulid DepartmentId   { get; set; }

        /// <summary>
        /// When false, this department is hidden for the given year
        /// (e.g. specializations not yet open to this cohort).
        /// </summary>
        public bool IsActive { get; set; } = true;

        // ── Navigation Properties ──────────────────────────────────────────────
        public AcademicYear AcademicYear { get; set; } = null!;
        public Department   Department   { get; set; } = null!;
    }
}
