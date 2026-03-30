using System;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using NUlid;
using UniversityManagementSystem.Core.Interfaces;

namespace UniversityManagementSystem.Infrastructure.Services
{
    /// <summary>
    /// Centralized JWT claim resolver injected into controllers and services.
    /// Handles all claim name patterns found in the system:
    ///   System user ID → ClaimTypes.NameIdentifier | "nameid" | "UserId"
    ///   Profile ID     → "ProfileId"
    ///   Role           → ClaimTypes.Role | "role"
    /// </summary>
    public class UserContextService(IHttpContextAccessor httpContextAccessor) : IUserContextService
    {
        private ClaimsPrincipal User =>
            httpContextAccessor.HttpContext?.User
            ?? throw new UnauthorizedAccessException("No HTTP context available. Ensure the request is authenticated.");

        // ── UserId ────────────────────────────────────────────────────────────
        public Ulid GetUserId()
        {
            var raw = GetUserIdString();
            if (!Ulid.TryParse(raw, out var uid))
                throw new UnauthorizedAccessException($"UserId claim '{raw}' is not a valid ULID.");
            return uid;
        }

        public string GetUserIdString()
        {
            var value =
                User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? User.FindFirst("nameid")?.Value
                ?? User.FindFirst("UserId")?.Value;

            if (string.IsNullOrEmpty(value))
                throw new UnauthorizedAccessException("UserId not found in token. Ensure ClaimTypes.NameIdentifier, 'nameid', or 'UserId' is set.");

            return value;
        }

        // ── ProfileId ─────────────────────────────────────────────────────────
        public Ulid GetProfileId()
        {
            var raw = TryGetProfileId()
                ?? throw new UnauthorizedAccessException("ProfileId claim not found in token.");

            if (!Ulid.TryParse(raw, out var pid))
                throw new UnauthorizedAccessException($"ProfileId claim '{raw}' is not a valid ULID.");

            return pid;
        }

        public string? TryGetProfileId() =>
            User.FindFirst("ProfileId")?.Value;

        // ── Role ──────────────────────────────────────────────────────────────
        public string GetRole()
        {
            var value =
                User.FindFirst(ClaimTypes.Role)?.Value
                ?? User.FindFirst("role")?.Value;

            if (string.IsNullOrEmpty(value))
                throw new UnauthorizedAccessException("Role not found in token.");

            return value;
        }

        public bool IsInRole(string role) =>
            User.IsInRole(role) ||
            string.Equals(GetRole(), role, StringComparison.OrdinalIgnoreCase);
    }
}
