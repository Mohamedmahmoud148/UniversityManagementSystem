using System;
using NUlid;

namespace UniversityManagementSystem.Core.Entities
{
    public abstract class BaseEntity
    {
        public Ulid Id { get; set; } = Ulid.NewUlid();
        public string Code { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? DeletedAt { get; set; }
    }
}
