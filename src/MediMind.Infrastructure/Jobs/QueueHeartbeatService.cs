using MediMind.Application.Features.Queue;
using MediMind.Domain.Common.Interfaces;
using MediMind.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MediMind.Infrastructure.Jobs;

/// <summary>
/// Pushes queue position updates to connected patients via SignalR every 30 seconds.
/// Satisfies the "update every 30 seconds via WebSocket" requirement.
/// </summary>
public class QueueHeartbeatService(
    IServiceScopeFactory scopeFactory,
    ILogger<QueueHeartbeatService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await TickAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Queue heartbeat tick failed");
            }

            await timer.WaitForNextTickAsync(stoppingToken);
        }
    }

    private async Task TickAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var queueRepository = scope.ServiceProvider.GetRequiredService<IQueueRepository>();
        var centerRepository = scope.ServiceProvider.GetRequiredService<IHealthcareCenterRepository>();
        var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var activeCenters = await centerRepository.GetActiveSubscriptionsAsync(ct);

        foreach (var center in activeCenters)
        {
            var entries = await queueRepository.GetCenterQueueAsync(center.Id, today);
            foreach (var entry in entries.Where(e =>
                e.Status == QueueStatus.Waiting || e.Status == QueueStatus.Called))
            {
                var appt = entry.Appointment;
                if (appt is null) continue;

                var statusMessage = entry.Status switch
                {
                    QueueStatus.Waiting =>
                        $"You are #{entry.Position} in queue, estimated wait: {QueueService.FormatWaitTime(entry.EstimatedWaitTimeMinutes)}",
                    QueueStatus.Called => "You've been called. Please proceed to your consultation room.",
                    _ => "Queue status updated"
                };

                var statusDto = new PatientQueueStatusDto(
                    entry.Id,
                    entry.QueueNumber,
                    entry.Position,
                    entry.EstimatedWaitTimeMinutes,
                    QueueService.FormatWaitTime(entry.EstimatedWaitTimeMinutes),
                    entry.Status.ToString(),
                    statusMessage,
                    new QueueCenterInfoDto(appt.Center?.CenterName ?? string.Empty, appt.Center?.Address ?? string.Empty),
                    new QueueDoctorInfoDto(appt.Doctor?.FullName ?? string.Empty, appt.Doctor?.Specialization ?? string.Empty),
                    appt.AppointmentTime);

                await notificationService.SendQueueUpdateToPatientAsync(appt.PatientId, statusDto);
            }
        }
    }
}
