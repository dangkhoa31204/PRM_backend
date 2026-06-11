using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace PRM_beckend.Hubs;

[Authorize(Roles = "1,2")]
public class StaffNotificationHub : Hub
{
    public const string Route = "/hubs/staff";
    public const string StaffGroup = "staff";

    public override async Task OnConnectedAsync()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, StaffGroup);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, StaffGroup);
        await base.OnDisconnectedAsync(exception);
    }
}
