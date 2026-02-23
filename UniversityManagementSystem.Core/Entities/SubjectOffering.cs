using System;

namespace UniversityManagementSystem.Core.Entities
{
    public class SubjectOffering : BaseEntity
    {
        public int SubjectId { get; private set; }
        public int SemesterId { get; private set; }
        public int DoctorId { get; private set; }
        public int DepartmentId { get; private set; }
        public int BatchId { get; private set; }
        public int? GroupId { get; private set; }
        public int MaxCapacity { get; private set; }

        public Subject Subject { get; private set; } = null!;
        public Semester Semester { get; private set; } = null!;
        public Doctor Doctor { get; private set; } = null!;
        public Department Department { get; private set; } = null!;
        public Batch Batch { get; private set; } = null!;
        public Group? Group { get; private set; }

        private SubjectOffering() { }

        public SubjectOffering(int subjectId, int semesterId, int doctorId, int departmentId, int batchId, int? groupId, int maxCapacity)
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
