using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace FamoBoard.Hubs
{
    [Authorize]
    public class BoardHub : Hub
    {
        public Task JoinBoard(string projectId) => Groups.AddToGroupAsync(Context.ConnectionId, $"project-{projectId}");
        public Task CardMoved(string projectId, int cardId, int toListId) =>
            Clients.OthersInGroup($"project-{projectId}").SendAsync("CardMoved", cardId, toListId);
    }
}
