using System;
using System.Collections.Generic;

namespace UniversityManagementSystem.Core.Entities
{
    public class AcademicYear : BaseEntity
    {
        public string Name { get; private set; } = null!; // e.g., "2025/2026"
        public bool IsActive { get; private set; }

        private AcademicYear() { } // EF Core

        public AcademicYear(string name, bool isActive)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentNullException(nameof(name));
            Name = name;
            IsActive = isActive;
            CreatedAt = DateTime.UtcNow;
        }

        public void Activate() => IsActive = true;
        public void Deactivate() => IsActive = false;

        public void Update(string name, bool isActive)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentNullException(nameof(name));
            Name = name;
            IsActive = isActive;
        }
    }
}
