using System;
using System.Collections.Generic;
using NUlid;

namespace UniversityManagementSystem.Core.Entities
{
    public class AcademicYear : BaseEntity
    {
        public string Name { get; private set; } = null!; // e.g., "First", "Second"
        public bool IsActive { get; private set; }

        /// <summary>
        /// Ordinal position within a college (1 = First Year, 2 = Second Year, …).
        /// Must be unique per college and within the range 1–6.
        /// </summary>
        public int Order { get; private set; }

        /// <summary>FK — which college this academic year belongs to.</summary>
        public Ulid CollegeId { get; private set; }

        // ── Navigation Properties ──────────────────────────────────────────────
        public College College { get; private set; } = null!;
        public ICollection<AcademicYearDepartment> AcademicYearDepartments { get; private set; } = [];

        private AcademicYear() { } // EF Core

        public AcademicYear(string name, bool isActive, int order, Ulid collegeId)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentNullException(nameof(name));
            if (order < 1 || order > 6) throw new ArgumentOutOfRangeException(nameof(order), "Order must be between 1 and 6.");
            Name = name;
            IsActive = isActive;
            Order = order;
            CollegeId = collegeId;
            CreatedAt = DateTime.UtcNow;
        }

        public void Activate() => IsActive = true;
        public void Deactivate() => IsActive = false;

        public void Update(string name, bool isActive, int order)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentNullException(nameof(name));
            if (order < 1 || order > 6) throw new ArgumentOutOfRangeException(nameof(order), "Order must be between 1 and 6.");
            Name = name;
            IsActive = isActive;
            Order = order;
        }
    }
}
