using MediMind.Domain.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace MediMind.Infrastructure.SignalR;

/// <summary>
/// SignalR Hub — provides WebSocket real-time queue updates.
/// Clients join a group by centerId to receive queue broadcasts.
/// NFR-003: Queue position updates every 30 seconds.
/// </summary>
[Authorize]
public class QueueHub(ILogger<QueueHub> logger) : Hub
{
    // Group naming convention: "queue_{centerId}"
    private static string CenterGroup(Guid centerId) => $"queue_{centerId}";
    // Patient-specific group: "patient_{userId}"
    private static string PatientGroup(Guid userId) => $"patient_{userId}";

    public override async Task OnConnectedAsync()
    {
        logger.LogInformation("Client connected: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        logger.LogInformation("Client disconnected: {ConnectionId}", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>Healthcare center admin joins center's queue group.</summary>
    public async Task JoinCenterQueue(string centerId)
    {
        if (Guid.TryParse(centerId, out _))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, CenterGroup(Guid.Parse(centerId)));
            logger.LogDebug("Connection {ConnectionId} joined center group {CenterId}",
                Context.ConnectionId, centerId);
        }
    }

    /// <summary>Patient joins their personal notification group.</summary>
    public async Task JoinPatientGroup(string userId)
    {
        if (Guid.TryParse(userId, out _))
            await Groups.AddToGroupAsync(Context.ConnectionId, PatientGroup(Guid.Parse(userId)));
    }

    public async Task LeaveCenterQueue(string centerId)
    {
        if (Guid.TryParse(centerId, out _))
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, CenterGroup(Guid.Parse(centerId)));
    }
}

/// <summary>
/// Service that broadcasts queue updates via SignalR from application layer.
/// Injected into command handlers via IQueueHubService interface.
/// </summary>
public class QueueHubService(IHubContext<QueueHub> hubContext) : IQueueHubService
{
    public async Task BroadcastQueueUpdateAsync(Guid centerId, object queueData, CancellationToken ct = default)
    {
        // Broadcast to ALL clients in the center's group
        await hubContext.Clients
            .Group($"queue_{centerId}")
            .SendAsync("QueueUpdated", queueData, ct);
    }

    public async Task NotifyPatientAsync(Guid patientId, string eventName, object data, CancellationToken ct = default)
    {
        // Send to specific patient's group
        await hubContext.Clients
            .Group($"patient_{patientId}")
            .SendAsync(eventName, data, ct);
    }
}
