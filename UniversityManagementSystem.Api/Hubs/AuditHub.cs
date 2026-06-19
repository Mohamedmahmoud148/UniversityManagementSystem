using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

namespace UniversityManagementSystem.Api.Hubs
{
    [Authorize(Roles = "SuperAdmin,Admin")]
    public class AuditHub : Hub
    {
        public override async Task OnConnectedAsync()
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, "admins");
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(System.Exception? exception)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, "admins");
            await base.OnDisconnectedAsync(exception);
        }
    }
}
