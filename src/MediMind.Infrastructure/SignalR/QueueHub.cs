using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace MediMind.Infrastructure.SignalR;

/// <summary>
/// SignalR hub for real-time queue updates.
/// </summary>
[Authorize]
public class QueueHub(ILogger<QueueHub> logger) : Hub
{
    /// <summary>
    /// Joins center admin/staff queue group.
    /// </summary>
    public async Task JoinCenterGroup(string centerId)
    {
        if (!Guid.TryParse(centerId, out var parsed))
            throw new HubException("Invalid center id.");

        await Groups.AddToGroupAsync(Context.ConnectionId, $"center_{parsed}");
        logger.LogDebug("Connection {ConnectionId} joined center group {CenterId}", Context.ConnectionId, parsed);
    }

    /// <summary>
    /// Joins authenticated patient group using JWT subject.
    /// </summary>
    public async Task JoinPatientGroup()
    {
        var userId = Context.UserIdentifier ??
                     Context.User?.FindFirst("sub")?.Value ??
                     Context.User?.FindFirst("user_id")?.Value;

        if (!Guid.TryParse(userId, out var parsed))
            throw new HubException("Unable to resolve patient id from token.");

        await Groups.AddToGroupAsync(Context.ConnectionId, $"patient_{parsed}");
    }

    /// <summary>
    /// Joins doctor queue group scoped to center.
    /// </summary>
    public async Task JoinDoctorGroup(string centerId)
    {
        var userId = Context.UserIdentifier ??
                     Context.User?.FindFirst("sub")?.Value ??
                     Context.User?.FindFirst("user_id")?.Value;

        if (!Guid.TryParse(userId, out var doctorId) || !Guid.TryParse(centerId, out var parsedCenterId))
            throw new HubException("Invalid doctor or center id.");

        await Groups.AddToGroupAsync(Context.ConnectionId, $"doctor_{doctorId}_{parsedCenterId}");
    }

    /// <summary>
    /// Leaves any named group.
    /// </summary>
    public async Task LeaveGroup(string groupName)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
    }
}
