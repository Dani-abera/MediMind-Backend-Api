using MediMind.Application.Features.VideoConsultations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace MediMind.Infrastructure.SignalR;

[Authorize]
public sealed class VideoConsultationHub(IVideoConsultationService consultationService) : Hub
{
    private static readonly Dictionary<string, (Guid ConsultationId, Guid UserId, string UserType, string UserName)> Connections = new();

    public async Task JoinConsultationRoom(string consultationId)
    {
        if (!Guid.TryParse(consultationId, out var cid))
            return;

        var userIdClaim = Context.User?.FindFirst("sub")?.Value ?? Context.User?.FindFirst("user_id")?.Value;
        var userType = Context.User?.FindFirst("user_type")?.Value ?? "Unknown";
        var userName = Context.User?.FindFirst("name")?.Value ?? "User";
        if (!Guid.TryParse(userIdClaim, out var userId))
            return;

        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(cid));
        lock (Connections)
            Connections[Context.ConnectionId] = (cid, userId, userType, userName);

        await consultationService.JoinConsultationAsync(cid, userId, userType, Context.ConnectionId);
        await Clients.OthersInGroup(GroupName(cid)).SendAsync("UserJoined", new
        {
            connectionId = Context.ConnectionId,
            userId,
            userType,
            userName
        });
    }

    public async Task LeaveConsultationRoom(string consultationId)
    {
        if (!Guid.TryParse(consultationId, out var cid))
            return;

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(cid));
        (Guid consultationId, Guid userId, string userType, string userName) connection;
        lock (Connections)
        {
            if (!Connections.TryGetValue(Context.ConnectionId, out connection))
                return;
            Connections.Remove(Context.ConnectionId);
        }

        await Clients.OthersInGroup(GroupName(cid)).SendAsync("UserLeft", new
        {
            connectionId = Context.ConnectionId,
            userId = connection.userId,
            userType = connection.userType
        });
    }

    public Task SendOffer(string consultationId, string targetConnectionId, string sdpOffer) =>
        Clients.Client(targetConnectionId).SendAsync("ReceiveOffer", Context.ConnectionId, sdpOffer);

    public Task SendAnswer(string consultationId, string targetConnectionId, string sdpAnswer) =>
        Clients.Client(targetConnectionId).SendAsync("ReceiveAnswer", Context.ConnectionId, sdpAnswer);

    public Task SendIceCandidate(string consultationId, string targetConnectionId, string candidate) =>
        Clients.Client(targetConnectionId).SendAsync("ReceiveIceCandidate", Context.ConnectionId, candidate);

    public async Task BroadcastMediaState(string consultationId, bool isMuted, bool isCameraEnabled)
    {
        if (!Guid.TryParse(consultationId, out var cid))
            return;
        lock (Connections)
        {
            if (!Connections.ContainsKey(Context.ConnectionId))
                return;
        }
        await Clients.OthersInGroup(GroupName(cid)).SendAsync("ParticipantMediaStateChanged", new
        {
            connectionId = Context.ConnectionId,
            isMuted,
            isCameraEnabled
        });
    }

    public async Task SendChatMessage(string consultationId, string content)
    {
        if (!Guid.TryParse(consultationId, out var cid))
            return;
        (Guid ConsultationId, Guid UserId, string UserType, string UserName) connection;
        lock (Connections)
        {
            if (!Connections.TryGetValue(Context.ConnectionId, out connection))
                return;
        }

        try
        {
            var message = await consultationService.SaveChatMessageAsync(cid, connection.UserId, connection.UserType, content);
            await Clients.Group(GroupName(cid)).SendAsync("ReceiveChatMessage", new
            {
                messageId = message.MessageId,
                senderId = message.SenderId,
                senderName = message.SenderName,
                content = message.Content,
                sentAt = message.SentAt
            });
        }
        catch
        {
            await Clients.Caller.SendAsync("MessageSendFailed", new { content });
        }
    }

    public Task<List<object>> GetRoomParticipants(string consultationId)
    {
        if (!Guid.TryParse(consultationId, out var cid))
            return Task.FromResult(new List<object>());

        lock (Connections)
        {
            var participants = Connections
                .Where(x => x.Value.ConsultationId == cid)
                .Select(x => (object)new
                {
                    connectionId = x.Key,
                    userId = x.Value.UserId,
                    userType = x.Value.UserType
                })
                .ToList();
            return Task.FromResult(participants);
        }
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        (Guid consultationId, Guid userId, string userType, string userName) connection;
        var found = true;
        lock (Connections)
        {
            if (!Connections.TryGetValue(Context.ConnectionId, out connection))
            {
                found = false;
            }
            else
            {
                Connections.Remove(Context.ConnectionId);
            }
        }
        if (!found)
        {
            await base.OnDisconnectedAsync(exception);
            return;
        }

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(connection.consultationId));
        await Clients.OthersInGroup(GroupName(connection.consultationId)).SendAsync("UserLeft", new
        {
            connectionId = Context.ConnectionId,
            userId = connection.userId,
            userType = connection.userType,
            reason = exception?.Message
        });
        await base.OnDisconnectedAsync(exception);
    }

    private static string GroupName(Guid consultationId) => $"consultation_{consultationId}";
}

public sealed class VideoConsultationHubNotifier(IHubContext<VideoConsultationHub> hubContext) : IVideoConsultationHubNotifier
{
    public Task NotifyConsultationEndedAsync(Guid consultationId, string reason, CancellationToken ct = default) =>
        hubContext.Clients.Group($"consultation_{consultationId}")
            .SendAsync("ConsultationEnded", new { reason }, ct);

    public Task NotifyQualityAlertAsync(Guid consultationId, Guid userId, string message, CancellationToken ct = default) =>
        hubContext.Clients.Group($"consultation_{consultationId}")
            .SendAsync("QualityAlert", new { userId, message, audioOnlyRecommended = true }, ct);
}
