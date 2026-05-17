using System.ComponentModel.DataAnnotations;
using NUlid;

namespace UniversityManagementSystem.Core.DTOs
{
    // ── AcademicYear DTOs ─────────────────────────────────────────────────────

    public class AcademicYearDto
    {
        public Ulid   Id         { get; set; }
        public string Name       { get; set; } = string.Empty;
        public bool   IsActive   { get; set; }

        /// <summary>Ordinal position within the college (1 = First Year, …, 6 = Sixth Year).</summary>
        public int    Order      { get; set; }
        public Ulid   CollegeId  { get; set; }
        public string CollegeName { get; set; } = string.Empty;
    }

    public class CreateAcademicYearDto
    {
        [Required]
        public string Name { get; set; } = string.Empty;

        public bool IsActive { get; set; }

        /// <summary>Ordinal position. Must be 1–6 and unique per college.</summary>
        [Required]
        [Range(1, 6, ErrorMessage = "Order must be between 1 and 6.")]
        public int Order { get; set; }

        /// <summary>The college this year belongs to.</summary>
        [Required]
        public Ulid CollegeId { get; set; }
    }

    public class UpdateAcademicYearDto
    {
        [Required]
        public string Name { get; set; } = string.Empty;

        public bool IsActive { get; set; }

        /// <summary>Ordinal position. Must be 1–6 and unique per college.</summary>
        [Required]
        [Range(1, 6, ErrorMessage = "Order must be between 1 and 6.")]
        public int Order { get; set; }
    }

    // ── AcademicYearDepartment (junction) DTOs ────────────────────────────────

    /// <summary>Response: one allowed department mapping for a year.</summary>
    public class AcademicYearDepartmentDto
    {
        public Ulid   MappingId      { get; set; }
        public Ulid   AcademicYearId { get; set; }
        public string YearName       { get; set; } = string.Empty;
        public Ulid   DepartmentId   { get; set; }
        public string DepartmentName { get; set; } = string.Empty;
        public string DepartmentCode { get; set; } = string.Empty;
        public bool   IsActive       { get; set; }
    }

    /// <summary>POST body — admin assigns a department to an academic year.</summary>
    public class AssignDepartmentToYearDto
    {
        [Required]
        public Ulid DepartmentId { get; set; }

        /// <summary>Whether the mapping is immediately active. Defaults to true.</summary>
        public bool IsActive { get; set; } = true;
    }

    /// <summary>PATCH body — admin toggles IsActive on an existing mapping.</summary>
    public class UpdateYearDepartmentDto
    {
        [Required]
        public bool IsActive { get; set; }
    }
}
