using MediMind.Domain.Common.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace MediMind.Infrastructure.SignalR;

/// <summary>
/// Queue hub broadcast adapter.
/// </summary>
public class QueueHubService(IHubContext<QueueHub> hubContext) : IQueueHubService
{
    public async Task BroadcastQueueUpdateAsync(Guid centerId, object queueData, CancellationToken ct = default)
    {
        await hubContext.Clients.Group($"center_{centerId}").SendAsync("QueueRefreshed", queueData, ct);
    }

    public async Task NotifyPatientAsync(Guid patientId, string eventName, object data, CancellationToken ct = default)
    {
        await hubContext.Clients.Group($"patient_{patientId}").SendAsync(eventName, data, ct);
    }
}
