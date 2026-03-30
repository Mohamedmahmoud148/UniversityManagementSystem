using NUlid;

namespace UniversityManagementSystem.Core.Interfaces
{
    /// <summary>
    /// Centralized, production-grade service for resolving authenticated user identity
    /// from the current HTTP request's JWT claims.
    ///
    /// Handles the 4 claim patterns observed in the codebase:
    ///   • System user:  ClaimTypes.NameIdentifier / "nameid" / "UserId"
    ///   • Profile:      "ProfileId"
    ///   • Role:         ClaimTypes.Role / "role"
    /// </summary>
    public interface IUserContextService
    {
        /// <summary>
        /// Returns the SystemUser ULID (sub / nameid claim).
        /// Throws <see cref="UnauthorizedAccessException"/> if not found.
        /// </summary>
        Ulid GetUserId();

        /// <summary>
        /// Returns the userId as raw string without parsing (useful where the raw value is needed).
        /// Throws <see cref="UnauthorizedAccessException"/> if not found.
        /// </summary>
        string GetUserIdString();

        /// <summary>
        /// Returns the ProfileId (student/doctor/admin entity ID).
        /// Throws <see cref="UnauthorizedAccessException"/> if not found.
        /// </summary>
        Ulid GetProfileId();

        /// <summary>
        /// Returns the ProfileId as raw string.
        /// Returns null if not present (some roles may not have ProfileId).
        /// </summary>
        string? TryGetProfileId();

        /// <summary>Returns the user role string. Throws if not found.</summary>
        string GetRole();

        /// <summary>Returns true if the user has the given role (case-insensitive).</summary>
        bool IsInRole(string role);
    }
}
