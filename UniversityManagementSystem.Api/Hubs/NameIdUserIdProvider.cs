using Microsoft.AspNetCore.SignalR;

namespace UniversityManagementSystem.Api.Hubs
{
    /// <summary>
    /// Tells SignalR to use the "nameid" JWT claim as the user identifier,
    /// matching how AuthService issues tokens (new Claim("nameid", user.Id)).
    /// Without this, Context.UserIdentifier returns null and group-based
    /// push notifications never reach the correct client.
    /// </summary>
    public class NameIdUserIdProvider : IUserIdProvider
    {
        public string? GetUserId(HubConnectionContext connection)
            => connection.User?.FindFirst("nameid")?.Value;
    }
}
