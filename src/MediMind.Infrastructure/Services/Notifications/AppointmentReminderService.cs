using MediMind.Domain.Common.Interfaces;
using MediMind.Infrastructure.SignalR;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MediMind.Infrastructure.Services.Notifications;

/// <summary>
/// Background service that sends appointment reminders every five minutes.
/// </summary>
public class AppointmentReminderService(
    IServiceScopeFactory scopeFactory,
    ILogger<AppointmentReminderService> logger) : BackgroundService
{
    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(5));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessReminderWindow(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Appointment reminder cycle failed");
            }

            await timer.WaitForNextTickAsync(stoppingToken);
        }
    }

    private async Task ProcessReminderWindow(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var appointmentRepository = scope.ServiceProvider.GetRequiredService<IAppointmentRepository>();
        var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
        var dbContext = scope.ServiceProvider.GetRequiredService<Data.MediMindDbContext>();

        var now = DateTime.UtcNow;
        var reminders24h = await appointmentRepository.GetUpcomingForReminderAsync(now.AddHours(24), ReminderType.TwentyFourHours);
        foreach (var appointment in reminders24h)
        {
            var message = $"Reminder: Your appointment with Dr. {appointment.Doctor.FullName} at {appointment.Center.CenterName} is tomorrow at {appointment.AppointmentTime:HH\\:mm}. Reply to reschedule.";
            await notificationService.SendSmsAsync(appointment.Patient.PhoneNumber, message);
            await notificationService.SendPushAsync(appointment.PatientId, "Appointment reminder", message, new { appointmentId = appointment.Id });
            appointment.MarkReminderSent(ReminderType.TwentyFourHours);
        }

        var reminders2h = await appointmentRepository.GetUpcomingForReminderAsync(now.AddHours(2), ReminderType.TwoHours);
        foreach (var appointment in reminders2h)
        {
            var body = $"Reminder: Appointment with Dr. {appointment.Doctor.FullName} at {appointment.Center.CenterName} in about 2 hours.";
            await notificationService.SendPushAsync(appointment.PatientId, "Appointment in 2 hours", body, new { appointmentId = appointment.Id });
            appointment.MarkReminderSent(ReminderType.TwoHours);
        }

        await dbContext.SaveChangesAsync(ct);
    }
}

/// <summary>
/// Bridge adapter for current push/SMS services.
/// </summary>
public class NotificationServiceAdapter(
    IHubContext<QueueHub> hubContext,
    ILogger<NotificationServiceAdapter> logger) : INotificationService
{
    public Task SendPushAsync(Guid userId, string title, string body, object? data = null)
    {
        logger.LogInformation("Push stub: user={UserId} title={Title} body={Body}", userId, title, body);
        return Task.CompletedTask;
    }

    public Task SendSmsAsync(string phoneNumber, string message)
    {
        logger.LogInformation("SMS stub: phone={PhoneNumber} message={Message}", phoneNumber, message);
        return Task.CompletedTask;
    }

    public async Task SendQueueUpdateToPatientAsync(Guid patientId, object status)
    {
        await hubContext.Clients
            .Group($"patient_{patientId}")
            .SendAsync("QueuePositionUpdated", status);
    }

    public async Task BroadcastQueueRefreshAsync(Guid centerId, object dashboard)
    {
        await hubContext.Clients
            .Group($"center_{centerId}")
            .SendAsync("QueueRefreshed", dashboard);
    }

    public async Task BroadcastQueueEventAsync(Guid centerId, string eventName, object payload)
    {
        await hubContext.Clients
            .Group($"center_{centerId}")
            .SendAsync(eventName, payload);
    }
}
