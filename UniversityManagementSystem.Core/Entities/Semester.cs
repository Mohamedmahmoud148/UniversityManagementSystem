using System;
using NUlid;

namespace UniversityManagementSystem.Core.Entities
{
    public class Semester : BaseEntity
    {
        public string Name { get; private set; } = null!; // e.g., "Fall 2025"
        public Ulid AcademicYearId { get; private set; }
        public DateTime StartDate { get; private set; }
        public DateTime EndDate { get; private set; }

        public AcademicYear AcademicYear { get; private set; } = null!;

        private Semester() { }

        public Semester(string name, Ulid academicYearId, DateTime startDate, DateTime endDate)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentNullException(nameof(name));
            if (endDate <= startDate) throw new ArgumentException("EndDate must be after StartDate");

            Name = name;
            AcademicYearId = academicYearId;
            StartDate = startDate;
            EndDate = endDate;
        }

        public void Update(string name, DateTime startDate, DateTime endDate)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentNullException(nameof(name));
            if (endDate <= startDate) throw new ArgumentException("EndDate must be after StartDate");

            Name = name;
            StartDate = startDate;
            EndDate = endDate;
        }
    }
}
