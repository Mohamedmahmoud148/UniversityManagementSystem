using System;
using NUlid;

namespace UniversityManagementSystem.Core.Entities
{
    public class SubjectOffering : BaseEntity
    {
        public Ulid SubjectId { get; private set; }
        public Ulid SemesterId { get; private set; }
        public Ulid DoctorId { get; private set; }
        public Ulid DepartmentId { get; private set; }
        public Ulid BatchId { get; private set; }
        public Ulid? GroupId { get; private set; }
        public int MaxCapacity { get; private set; }

        public Subject Subject { get; private set; } = null!;
        public Semester Semester { get; private set; } = null!;
        public Doctor Doctor { get; private set; } = null!;
        public Department Department { get; private set; } = null!;
        public Batch Batch { get; private set; } = null!;
        public Group? Group { get; private set; }

        private SubjectOffering() { }

        public SubjectOffering(Ulid subjectId, Ulid semesterId, Ulid doctorId, Ulid departmentId, Ulid batchId, Ulid? groupId, int maxCapacity)
        {
            if (maxCapacity <= 0) throw new ArgumentException("Max capacity must be greater than zero.");

            SubjectId = subjectId;
            SemesterId = semesterId;
            DoctorId = doctorId;
            DepartmentId = departmentId;
            BatchId = batchId;
            GroupId = groupId;
            MaxCapacity = maxCapacity;
            CreatedAt = DateTime.UtcNow;
        }
    }
}
