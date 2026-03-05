using System;
using NUlid;

namespace UniversityManagementSystem.Core.Entities
{
    public class RefreshToken : BaseEntity
    {
        public string Token { get; set; } = string.Empty;
        public string JwtId { get; set; } = string.Empty;
        public DateTime CreationDate { get; set; }
        public DateTime ExpiryDate { get; set; }
        public bool Used { get; set; }
        public bool Invalidated { get; set; }
        public Ulid UserId { get; set; }

        public SystemUser User { get; set; } = null!;
    }
}
