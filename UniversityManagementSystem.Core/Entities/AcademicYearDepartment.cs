using System;
using NUlid;

namespace UniversityManagementSystem.Core.Entities
{
    /// <summary>
    /// Junction table that controls which departments are available for a given academic year.
    /// IsActive lets admins toggle availability without removing the mapping record.
    ///
    /// Deliberately does NOT inherit BaseEntity:
    ///   • It is a configuration/mapping record, not a business entity.
    ///   • Soft-delete semantics do not apply — removal is always a true hard delete.
    ///   • No Code field is needed; the composite (AcademicYearId, DepartmentId) is the natural key.
    /// </summary>
    public class AcademicYearDepartment
    {
        /// <summary>Surrogate PK — ULID, stable reference for PATCH/DELETE by mapping ID.</summary>
        public Ulid Id { get; set; } = Ulid.NewUlid();

        public Ulid AcademicYearId { get; set; }
        public Ulid DepartmentId   { get; set; }

        /// <summary>
        /// When false, this department is hidden for the given year
        /// (e.g. specializations not yet open to this cohort).
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>Timestamp for auditing — when was this mapping created.</summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // ── Navigation Properties ──────────────────────────────────────────────
        public AcademicYear AcademicYear { get; set; } = null!;
        public Department   Department   { get; set; } = null!;
    }
}
